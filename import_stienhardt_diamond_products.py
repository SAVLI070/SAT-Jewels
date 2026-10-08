import os
import io
import re
import sys
import time
import base64
import requests
import psycopg2
from PIL import Image
import cloudinary
import cloudinary.uploader
from concurrent.futures import ThreadPoolExecutor, as_completed

# Force unbuffered stdout
sys.stdout.reconfigure(line_buffering=True)

cloudinary.config(
  cloud_name = 'ihcs8m6o',
  api_key = '826999397858529',
  api_secret = 'S8guO0Os21rzu4vxKAztT39irto'
)

DB_URL = "postgresql://satjewels_admin:SatJewels%23Db2026%21Secure@satjewels-postgres.c4r4s48oeqi1.us-east-1.rds.amazonaws.com:5432/satjewels_db"
API_URL = "https://pxixbvvukwwmhmbyzdfu.supabase.co/functions/v1/storefront-api/search"
AUTH_HEADER = "Basic " + base64.b64encode(b"fooPalawat:barPalawat").decode("utf-8")

HEADERS = {
    "Content-Type": "application/json",
    "Authorization": AUTH_HEADER,
    "User-Agent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)"
}

# Only include diamond shapes existing in our system
SUPPORTED_SHAPES = {
    'Round': 1,
    'Oval': 2,
    'Emerald': 3,
    'Marquise': 4,
    'Pear': 5,
    'Princess': 6,
    'Cushion': 7,
    'Radiant': 8,
    'Asscher': 9
}

def log(msg):
    print(msg, flush=True)

def fetch_diamonds_for_shape(shape_name):
    items = []
    for page in range(1, 4):
        body = {
            'type': 'LabGrown',
            'diamondtype': shape_name,
            'diamondcolor': '',
            'diamondclarity': '',
            'diamondcut': '',
            'reportby': '',
            'caratfrom': '',
            'caratto': '',
            'pricefrom': '',
            'priceto': ''
        }
        try:
            r = requests.post(f"{API_URL}?page={page}", json=body, headers=HEADERS, timeout=8)
            if r.status_code == 200:
                data = r.json()
                diamonds_obj = data.get('Diamonds', {})
                batch = diamonds_obj.get('data', [])
                if not batch:
                    break
                items.extend(batch)
                last_page = diamonds_obj.get('last_page', 1)
                if page >= last_page:
                    break
            else:
                log(f"API {shape_name} p{page} returned {r.status_code}")
                break
        except Exception as e:
            log(f"API {shape_name} p{page} error: {e}")
            break
    return items

def process_and_upload_image(item):
    shape_slug = item['target_shape_name'].lower()
    sku = (item.get('sku') or f"DIA_{int(time.time()*1000)%1000000}").strip()
    
    img_url = None
    shopify_res = item.get('shopify_res')
    if isinstance(shopify_res, dict) and 'product' in shopify_res:
        p_obj = shopify_res['product']
        if isinstance(p_obj, dict) and 'image' in p_obj and p_obj['image']:
            img_url = p_obj['image'].get('src')

    if not img_url:
        img_url = item.get('cover_pic')

    if img_url and 'gem360.html' in img_url:
        img_url = re.sub(r'^(https?://[^/]+)/gem360\.html\?d=([^&]+).*$', r'\1/imaged/\2/still.jpg', img_url)

    if not img_url:
        return (item, "")

    try:
        resp = requests.get(img_url, headers={"User-Agent": "Mozilla/5.0"}, timeout=6)
        if resp.status_code == 200 and resp.content:
            im = Image.open(io.BytesIO(resp.content)).convert('RGB')
            im.thumbnail((1000, 1000), Image.Resampling.LANCZOS)
            buf = io.BytesIO()
            im.save(buf, format='JPEG', quality=84, optimize=True)
            buf.seek(0)

            sku_clean = re.sub(r'[^a-zA-Z0-9_\-]', '_', sku.lower()).strip('_')
            pub_id = f"{shape_slug}_{sku_clean}_{int(time.time()*1000)%100000}"

            res = cloudinary.uploader.upload(
                buf,
                folder="sat_jewels/catalog/diamonds",
                public_id=pub_id,
                overwrite=True
            )
            cloud_url = res.get('secure_url') or img_url
            return (item, cloud_url)
        else:
            return (item, img_url)
    except Exception as e:
        return (item, img_url)

def main():
    log("=== STARTING DIAMOND PRODUCT IMPORT ===")
    conn = psycopg2.connect(DB_URL)
    conn.autocommit = False
    cur = conn.cursor()

    # Sync sequences
    cur.execute("""
        DO $$
        BEGIN
            BEGIN PERFORM setval(pg_get_serial_sequence('products', 'id'), COALESCE((SELECT MAX(id) FROM products), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
            BEGIN PERFORM setval(pg_get_serial_sequence('product_variants', 'id'), COALESCE((SELECT MAX(id) FROM product_variants), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
            BEGIN PERFORM setval(pg_get_serial_sequence('product_images', 'id'), COALESCE((SELECT MAX(id) FROM product_images), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
        END $$;
    """)
    conn.commit()

    cur.execute("SELECT sku FROM product_variants WHERE sku IS NOT NULL;")
    existing_skus = set(row[0].upper() for row in cur.fetchall())
    log(f"Existing variant SKUs in DB: {len(existing_skus)}")

    all_diamonds = []
    log("\nFetching products from Stienhardt Storefront API...")
    for shape_name, shape_id in SUPPORTED_SHAPES.items():
        diamonds = fetch_diamonds_for_shape(shape_name)
        log(f"  + {shape_name:10} (Shape ID {shape_id:2}): {len(diamonds)} diamonds")
        for d in diamonds:
            d['target_shape_id'] = shape_id
            d['target_shape_name'] = shape_name
            all_diamonds.append(d)

    log(f"Total diamonds fetched: {len(all_diamonds)}")

    diamonds_to_import = []
    for d in all_diamonds:
        sku = (d.get('sku') or '').strip()
        sku_key = f"SAT-DIA-{sku}".upper()
        if sku_key in existing_skus:
            continue
        diamonds_to_import.append(d)

    log(f"New diamonds to insert: {len(diamonds_to_import)}")

    if not diamonds_to_import:
        log("No new diamonds to import. Database is completely up to date.")
        cur.close()
        conn.close()
        return

    log("\nUploading product images and inserting into Database...")
    inserted_count = 0
    batch_size = 30

    for b_idx in range(0, len(diamonds_to_import), batch_size):
        batch = diamonds_to_import[b_idx:b_idx + batch_size]
        uploaded_items = []
        with ThreadPoolExecutor(max_workers=15) as executor:
            future_to_item = {executor.submit(process_and_upload_image, item): item for item in batch}
            for future in as_completed(future_to_item):
                try:
                    uploaded_items.append(future.result())
                except Exception as ex:
                    log(f"Error processing image: {ex}")

        for d, img_path in uploaded_items:
            sku = (d.get('sku') or f"DIA_{int(time.time()*1000)%1000000}").strip()
            title = (d.get('title') or '').strip()
            shape_id = d.get('target_shape_id')
            carat = d.get('carat') or 1.0
            color = (d.get('color') or 'F').strip()
            clarity = (d.get('clarity') or 'VS1').strip()
            cut = (d.get('cut') or 'Excellent').strip()
            lab = (d.get('lab') or 'IGI').strip()
            cert_num = (d.get('cert_num') or '').strip()
            measurement = (d.get('measurement') or '').strip()
            price = float(d.get('original_price') or d.get('sale_price') or 500)

            clean_slug = re.sub(r'[^a-z0-9\-]', '-', title.lower()).strip('-')
            clean_slug = re.sub(r'\-+', '-', clean_slug)[:55]
            unique_slug = f"{clean_slug}-{sku.lower()}-{int(time.time()*1000)%100000}"

            spec_str = f"{carat}ct | Color {color} | Clarity {clarity} | Cut {cut} | {lab} #{cert_num} | Measurements: {measurement} mm | Loose Diamond"

            try:
                # 1. Insert product
                cur.execute("""
                    INSERT INTO products (title, slug, price, category_id, diamond_shape_id, created_at, image_path, is_active)
                    VALUES (%s, %s, %s, 7, %s, NOW(), %s, TRUE)
                    RETURNING id;
                """, (title, unique_slug, price, shape_id, img_path))
                prod_id = cur.fetchone()[0]

                # 2. Insert image
                if img_path:
                    cur.execute("""
                        INSERT INTO product_images (product_id, image_path, display_order)
                        VALUES (%s, %s, 1);
                    """, (prod_id, img_path))

                # 3. Insert variant
                var_sku = f"SAT-DIA-{sku.upper()}"
                cur.execute("""
                    INSERT INTO product_variants (product_id, metal_id, carat_id, sku, price, stock_quantity, is_available)
                    VALUES (%s, 1, 3, %s, %s, 10, TRUE);
                """, (prod_id, var_sku, price))

                # 4. Insert CatalogItem
                catalog_id = f"sat-prod-{prod_id}"
                cur.execute("""
                    INSERT INTO "CatalogItems" ("Id", "Name", "CategoryId", "Spec", "PriceUSD", "Price", "ImageUrl", "GalleryImages", "MetalOptions", "CaratOptions", "IsActive", "CreatedAt")
                    VALUES (%s, %s, '7', %s, %s, %s, %s, %s, '', '', TRUE, NOW());
                """, (catalog_id, title, spec_str, price, price, img_path, img_path))

                conn.commit()
                inserted_count += 1
                existing_skus.add(var_sku)
            except Exception as e:
                conn.rollback()
                log(f"Failed inserting {sku} ({title}): {e}")

        log(f"Progress: [{inserted_count}/{len(diamonds_to_import)}] diamonds imported into Database...")

    cur.close()
    conn.close()

    log("\n" + "="*50)
    log(f"SUCCESS: Inserted {inserted_count} Loose Diamonds across all matching shapes into Database!")
    log("="*50)

if __name__ == '__main__':
    main()
