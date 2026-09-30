import os
import io
import re
import sys
import time
import json
import urllib.request
import psycopg2
import cloudinary
import cloudinary.uploader
from concurrent.futures import ThreadPoolExecutor, as_completed

cloudinary.config(
  cloud_name = 'ihcs8m6o',
  api_key = '826999397858529',
  api_secret = 'S8guO0Os21rzu4vxKAztT39irto'
)

DB_URL = "postgresql://satjewels_admin:SatJewels%23Db2026%21Secure@satjewels-postgres.c4r4s48oeqi1.us-east-1.rds.amazonaws.com:5432/satjewels_db"

def fetch_stienhardt_diamonds(pages=3):
    base_url = 'https://pxixbvvukwwmhmbyzdfu.supabase.co/functions/v1/storefront-api/search?page='
    headers = {
        'Authorization': 'Basic Zm9vUGFsYXdhdDpiYXJQYWxhd2F0',
        'Content-Type': 'application/json',
        'User-Agent': 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36'
    }
    
    all_diamonds = []
    seen_skus = set()

    for p in range(1, pages + 1):
        payload = json.dumps({
            'type': 'LabGrown',
            'diamondtype': 'Emerald',
            'sortby': 'Recommended-ASC'
        }).encode('utf-8')

        req = urllib.request.Request(base_url + str(p), data=payload, headers=headers)
        try:
            with urllib.request.urlopen(req, timeout=15) as resp:
                data = json.loads(resp.read().decode('utf-8'))
                d_dict = data.get('Diamonds', {})
                items = d_dict.get('data', [])
                for item in items:
                    sku = item.get('sku') or str(item.get('id'))
                    if sku not in seen_skus:
                        seen_skus.add(sku)
                        all_diamonds.append(item)
                print(f"Fetched page {p}: found {len(items)} items. Total unique diamonds: {len(all_diamonds)}")
        except Exception as e:
            print(f"Error fetching page {p}: {e}")

    return all_diamonds

def download_and_upload_worker(item):
    sku = item.get('sku') or str(item.get('id'))
    img_url = item.get('cover_pic')
    if not img_url:
        shopify_res = item.get('shopify_res') or {}
        img_url = shopify_res.get('product', {}).get('image', {}).get('src')
    
    if not img_url:
        return sku, None

    headers = {'User-Agent': 'Mozilla/5.0'}
    clean_sku = re.sub(r'[^a-zA-Z0-9_-]', '_', sku)
    
    try:
        req = urllib.request.Request(img_url, headers=headers)
        with urllib.request.urlopen(req, timeout=12) as resp:
            img_bytes = resp.read()

        if len(img_bytes) < 1000:
            return sku, None

        upload_res = cloudinary.uploader.upload(
            img_bytes,
            folder='sat_jewels/catalog/diamonds',
            public_id=f"emerald_{clean_sku.lower()}",
            overwrite=True,
            resource_type='image'
        )
        return sku, upload_res.get('secure_url')
    except Exception as e:
        print(f"Failed upload for {sku} ({img_url}): {e}")
        return sku, None

def main():
    print("Fetching Emerald Cut Lab-Grown Diamonds from Stienhardt API...")
    diamonds = fetch_stienhardt_diamonds(pages=3)
    print(f"Total diamonds to process: {len(diamonds)}")

    print("\nStarting concurrent Cloudinary image uploads (8 workers)...")
    uploaded_map = {}
    with ThreadPoolExecutor(max_workers=8) as executor:
        futures = {executor.submit(download_and_upload_worker, it): it for it in diamonds}
        done = 0
        for f in as_completed(futures):
            sku, c_url = f.result()
            if c_url:
                uploaded_map[sku] = c_url
            done += 1
            if done % 10 == 0 or done == len(diamonds):
                print(f"  -> Uploaded {len(uploaded_map)}/{done} diamond images...")

    print(f"\nAll images processed. Successfully uploaded {len(uploaded_map)} images to Cloudinary.")

    # Connect to DB
    conn = psycopg2.connect(DB_URL)
    cur = conn.cursor()

    # 1. Ensure Category "Diamonds" (ID 7) exists
    cur.execute("SELECT id FROM categories WHERE id = 7 OR LOWER(slug) = 'diamonds';")
    cat_row = cur.fetchone()
    if not cat_row:
        print("Inserting Category 'Diamonds' with ID 7...")
        cur.execute("""
            INSERT INTO categories (id, name, slug)
            VALUES (7, 'Diamonds', 'diamonds')
            ON CONFLICT (id) DO UPDATE SET name = 'Diamonds', slug = 'diamonds';
        """)
        conn.commit()
        cat_id = 7
    else:
        cat_id = cat_row[0]
        print(f"Category 'Diamonds' already exists with ID: {cat_id}")

    # Set sequence if needed
    cur.execute("SELECT setval('categories_id_seq', (SELECT GREATEST(MAX(id), 7) FROM categories));")
    conn.commit()

    inserted_count = 0
    print("\nInserting diamonds into PostgreSQL products and CatalogItems...")

    for item in diamonds:
        sku = item.get('sku') or str(item.get('id'))
        c_url = uploaded_map.get(sku)
        if not c_url:
            continue

        raw_price = float(item.get('original_price') or item.get('sale_price') or 500.0)
        price = round(raw_price, 2)
        carat = float(item.get('carat') or 1.0)
        color = item.get('color') or 'F'
        clarity = item.get('clarity') or 'VS1'
        cut = item.get('cut') or 'Excellent'
        lab = item.get('lab') or 'IGI'
        cert_num = item.get('cert_num') or ''
        measurement = item.get('measurement') or ''
        title = item.get('title') or f"{carat:.2f} Carat Emerald {lab} Certified Lab Grown Diamond"

        clean_slug = re.sub(r'[^a-z0-9\-]', '-', title.lower()).strip('-')
        clean_slug = re.sub(r'\-+', '-', clean_slug)[:50]
        unique_slug = f"{clean_slug}-stien-{sku.lower()}"
        unique_slug = re.sub(r'[^a-z0-9\-]', '-', unique_slug)[:80]

        spec_str = f"{carat:.2f}ct | Color {color} | Clarity {clarity} | Cut {cut} | {lab} #{cert_num} | Measurements: {measurement} mm | Loose Diamond"

        try:
            # 1. products table (shape_id 3 = Emerald)
            cur.execute("""
                INSERT INTO products (title, slug, price, category_id, diamond_shape_id, created_at, image_path)
                VALUES (%s, %s, %s, %s, %s, NOW(), %s)
                RETURNING id;
            """, (title, unique_slug, price, cat_id, 3, c_url))
            prod_id = cur.fetchone()[0]

            # 2. product_images
            cur.execute("""
                INSERT INTO product_images (product_id, image_path, display_order)
                VALUES (%s, %s, 1);
            """, (prod_id, c_url))

            # 3. product_variants (single loose gem variant, metal_id 1 default, no offsets)
            var_sku = f"SAT-DIA-{sku}"
            cur.execute("""
                INSERT INTO product_variants (product_id, metal_id, carat_id, sku, price, stock_quantity, is_available)
                VALUES (%s, 1, 3, %s, %s, 10, TRUE);
            """, (prod_id, var_sku, price))

            # 4. CatalogItems (No metal options, no carat options per task specification!)
            catalog_id = f"sat-prod-{prod_id}"
            cur.execute("""
                INSERT INTO "CatalogItems" ("Id", "Name", "CategoryId", "Spec", "PriceUSD", "Price", "ImageUrl", "GalleryImages", "MetalOptions", "CaratOptions", "IsActive", "CreatedAt")
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, TRUE, NOW())
                ON CONFLICT ("Id") DO UPDATE SET
                    "Name" = EXCLUDED."Name",
                    "CategoryId" = EXCLUDED."CategoryId",
                    "Spec" = EXCLUDED."Spec",
                    "PriceUSD" = EXCLUDED."PriceUSD",
                    "Price" = EXCLUDED."Price",
                    "ImageUrl" = EXCLUDED."ImageUrl",
                    "GalleryImages" = EXCLUDED."GalleryImages",
                    "MetalOptions" = EXCLUDED."MetalOptions",
                    "CaratOptions" = EXCLUDED."CaratOptions";
            """, (catalog_id, title, str(cat_id), spec_str, price, price, c_url, c_url, "", ""))

            conn.commit()
            inserted_count += 1
        except Exception as e:
            conn.rollback()
            print(f"Error inserting diamond {sku}: {e}")

    cur.close()
    conn.close()

    print(f"\n========================================================")
    print(f"SUCCESS! Ingested {inserted_count} Emerald Cut Lab Diamonds into SAT Jewel Category 'Diamonds'!")
    print(f"========================================================")

if __name__ == '__main__':
    main()
