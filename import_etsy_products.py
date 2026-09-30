import os
import io
import re
import sys
import time
import urllib.request
from bs4 import BeautifulSoup
from PIL import Image
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

def is_valid_ring(title, slug):
    text = (title + ' ' + slug).lower()
    if any(bad in text for bad in ['earring', 'stud', 'pendant', 'necklace', 'bracelet', 'bangle', 'chain', 'cufflink']):
        return False
    if any(good in text for good in ['ring', 'band', 'solitaire', 'eternity', 'bridal', 'trilogy', 'halo']):
        return True
    return False

def scrape_etsy_rings(max_pages=5):
    unique_items = {}
    for p in range(1, max_pages + 1):
        url = f'https://www.etsy.com/in-en/shop/TheAmericanGoldHouse?section_id=all&page={p}'
        req = urllib.request.Request(url, headers={
            'User-Agent': 'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
            'Accept-Language': 'en-US,en;q=0.9',
            'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8'
        })
        try:
            with urllib.request.urlopen(req, timeout=12) as resp:
                html = resp.read().decode('utf-8', errors='ignore')
                soup = BeautifulSoup(html, 'html.parser')
                for a in soup.find_all('a', href=True):
                    href = a['href']
                    m = re.search(r'/listing/(\d+)/([^/?#]+)', href)
                    if m:
                        lid, slug = m.group(1), m.group(2)
                        title = a.get('title') or a.get_text(strip=True)
                        img = a.find('img')
                        if img and lid not in unique_items:
                            img_src = img.get('src') or img.get('data-src') or ''
                            if img_src and 'etsystatic' in img_src and is_valid_ring(title, slug):
                                unique_items[lid] = {
                                    'id': lid,
                                    'slug': slug,
                                    'raw_title': title,
                                    'thumb_url': img_src
                                }
                print(f"Scraped page {p}, total valid rings collected: {len(unique_items)}")
        except Exception as e:
            print(f"Error scraping page {p}: {e}")
        time.sleep(0.5)
    return list(unique_items.values())

def format_luxury_title(raw_title, shape_name, cat_name):
    clean = raw_title.replace('TheAmericanGoldHouse', '').strip()
    # Split by comma or slash
    chunks = [c.strip() for c in re.split(r'[,|/]', clean) if len(c.strip()) > 3]
    
    # Filter out noisy marketing phrases
    good_chunks = []
    for c in chunks:
        low = c.lower()
        if any(skip in low for skip in ['gift for', 'matching band for her', 'bride', 'anniversary', 'promise ring gift', 'hand made', 'etsy', 'stacking ring for gift']):
            continue
        good_chunks.append(c)
    
    if good_chunks:
        title_candidate = good_chunks[0]
        # Clean carat prefix like "2.75 TC " or "14k Solid Yellow Gold "
        title_candidate = re.sub(r'^(10k|14k|18k)\s+(solid\s+)?(yellow|white|rose)?\s*gold\s*', '', title_candidate, flags=re.I).strip()
        title_candidate = re.sub(r'^\d+(\.\d+)?\s*(TC|CT|TCW|Total CT|tc|ct|tcw)?\s*', '', title_candidate, flags=re.I).strip()
        if len(title_candidate) >= 8:
            # Capitalize words
            return ' '.join(w.capitalize() for w in title_candidate.split())[:75]

    return f"{shape_name} Cut Lab Grown Diamond {cat_name}"

def detect_shape_and_category(title, slug):
    text = (title + " " + slug).lower()
    
    # Shape detection
    shape_id = 1
    shape_name = "Round"
    if 'emerald' in text or 'baguette' in text:
        shape_id = 3
        shape_name = "Emerald"
    elif 'oval' in text or 'moval' in text:
        shape_id = 2
        shape_name = "Oval"
    elif 'radiant' in text:
        shape_id = 8
        shape_name = "Radiant"
    elif 'pear' in text or 'teardrop' in text:
        shape_id = 5
        shape_name = "Pear"
    elif 'marquise' in text:
        shape_id = 4
        shape_name = "Marquise"
    elif 'cushion' in text:
        shape_id = 7
        shape_name = "Cushion"
    elif 'princess' in text:
        shape_id = 6
        shape_name = "Princess"
    elif 'asscher' in text:
        shape_id = 9
        shape_name = "Asscher"
    elif 'heart' in text:
        shape_id = 10
        shape_name = "Heart"
    elif 'round' in text or 'brilliant' in text:
        shape_id = 1
        shape_name = "Round"
        
    # Category detection
    cat_id = 1
    cat_name = "Engagement Ring"
    if any(m in text for m in ["men's", "mens", "signet", "man ring", "men ring"]):
        cat_id = 3
        cat_name = "Men's Wedding Band"
    elif any(w in text for w in ["wedding band", "eternity band", "stacking band", "chevron band", "matching band", "curved band"]):
        cat_id = 2
        cat_name = "Wedding Band"
    else:
        cat_id = 1
        cat_name = "Engagement Ring"
        
    # Carat extraction
    carat_match = re.search(r'(\d+(\.\d+)?)\s*(ct|tc|carat|tcw)', text)
    carat_val = 1.5
    if carat_match:
        try:
            cv = float(carat_match.group(1))
            if 0.3 <= cv <= 6.0:
                carat_val = cv
        except:
            pass

    return shape_id, shape_name, cat_id, cat_name, carat_val

def upload_worker(item):
    lid = item['id']
    thumb = item['thumb_url']
    high_res = re.sub(r'il_\d+x\d+', 'il_fullxfull', thumb)
    try:
        req = urllib.request.Request(high_res, headers={'User-Agent': 'Mozilla/5.0'})
        with urllib.request.urlopen(req, timeout=12) as resp:
            data = resp.read()
        im = Image.open(io.BytesIO(data)).convert('RGB')
        im.thumbnail((1920, 1920), Image.Resampling.LANCZOS)
        buf = io.BytesIO()
        im.save(buf, format='JPEG', quality=90, optimize=True)
        buf.seek(0)
        up = cloudinary.uploader.upload(
            buf,
            folder='sat_jewels/catalog/etsy',
            public_id=f"etsy_{lid}",
            overwrite=True,
            resource_type='image'
        )
        return lid, up.get('secure_url')
    except Exception as e:
        # Fallback to thumbnail
        try:
            req = urllib.request.Request(thumb, headers={'User-Agent': 'Mozilla/5.0'})
            with urllib.request.urlopen(req, timeout=10) as resp:
                data = resp.read()
            buf = io.BytesIO(data)
            up = cloudinary.uploader.upload(
                buf,
                folder='sat_jewels/catalog/etsy',
                public_id=f"etsy_{lid}",
                overwrite=True,
                resource_type='image'
            )
            return lid, up.get('secure_url')
        except Exception as e2:
            return lid, None

def main():
    print("=== Scraping Etsy TheAmericanGoldHouse Rings ===")
    rings = scrape_etsy_rings(max_pages=5)
    print(f"Total valid rings scraped: {len(rings)}")
    
    # Target 125 rings
    target_rings = rings[:125]
    print(f"Targeting {len(target_rings)} rings for Cloudinary upload and DB insertion...")
    
    print("\nStarting concurrent Cloudinary image uploads (8 parallel workers)...")
    uploaded_map = {}
    with ThreadPoolExecutor(max_workers=8) as executor:
        future_to_item = {executor.submit(upload_worker, it): it for it in target_rings}
        done_count = 0
        for future in as_completed(future_to_item):
            lid, c_url = future.result()
            if c_url:
                uploaded_map[lid] = c_url
            done_count += 1
            if done_count % 15 == 0 or done_count == len(target_rings):
                print(f"  -> Uploaded {len(uploaded_map)}/{done_count} images...")

    print(f"\nAll images processed. Successfully uploaded {len(uploaded_map)} images to Cloudinary.")
    
    conn = psycopg2.connect(DB_URL)
    cur = conn.cursor()

    configured_metals = [
        (1, '10K Yellow Gold', 0),
        (2, '10K White Gold', 0),
        (3, '10K Rose Gold', 0),
        (4, '14K Yellow Gold', 200),
        (5, '14K White Gold', 200),
        (6, '14K Rose Gold', 200),
        (7, '18K Yellow Gold', 450),
        (8, '18K White Gold', 450),
        (9, '18K Rose Gold', 450),
        (10, '950 Platinum', 650)
    ]
    metal_options_str = "10K Yellow Gold|10K White Gold|10K Rose Gold|14K Yellow Gold (+200 USD)|14K White Gold (+200 USD)|14K Rose Gold (+200 USD)|18K Yellow Gold (+450 USD)|18K White Gold (+450 USD)|18K Rose Gold (+450 USD)|950 Platinum (+650 USD)"

    inserted_count = 0
    print("\nInserting rings into SAT Jewels Database...")

    for it in target_rings:
        lid = it['id']
        cloud_url = uploaded_map.get(lid)
        if not cloud_url:
            continue
            
        shape_id, shape_name, cat_id, cat_name, carat_val = detect_shape_and_category(it['raw_title'], it['slug'])
        title = format_luxury_title(it['raw_title'], shape_name, cat_name)

        if cat_id == 2:
            base_price = 850.0 if carat_val < 1.0 else 1150.0
            carat_ids = [1, 2, 3]
            carat_options_str = "0.50 CT|0.75 CT (+150 USD)|1.00 CT (+300 USD)"
        elif cat_id == 3:
            base_price = 1150.0
            carat_ids = [2, 3, 5]
            carat_options_str = "0.75 CT|1.00 CT (+250 USD)|1.50 CT (+550 USD)"
        else:
            if carat_val <= 1.0:
                base_price = 1250.0
                carat_ids = [3, 5, 6]
                carat_options_str = "1.00 CT|1.50 CT (+450 USD)|2.00 CT (+950 USD)"
            elif carat_val <= 2.0:
                base_price = 1750.0
                carat_ids = [5, 6, 7]
                carat_options_str = "1.50 CT|2.00 CT (+500 USD)|3.00 CT (+1200 USD)"
            else:
                base_price = 2450.0
                carat_ids = [6, 7, 8]
                carat_options_str = "2.00 CT|3.00 CT (+700 USD)|4.00 CT (+1500 USD)"

        clean_slug = re.sub(r'[^a-z0-9\-]', '-', title.lower()).strip('-')
        clean_slug = re.sub(r'\-+', '-', clean_slug)[:50]
        unique_slug = f"{clean_slug}-etsy-{lid}"

        try:
            # 1. products
            cur.execute("""
                INSERT INTO products (title, slug, price, category_id, diamond_shape_id, created_at, image_path)
                VALUES (%s, %s, %s, %s, %s, NOW(), %s)
                RETURNING id;
            """, (title, unique_slug, base_price, cat_id, shape_id, cloud_url))
            prod_id = cur.fetchone()[0]

            # 2. product_images
            cur.execute("""
                INSERT INTO product_images (product_id, image_path, display_order)
                VALUES (%s, %s, 1);
            """, (prod_id, cloud_url))

            # 3. product_variants
            sku_idx = 1
            for m_id, m_name, m_offset in configured_metals:
                for c_id in carat_ids:
                    c_offset = 0
                    if c_id == 5 and 3 in carat_ids: c_offset = 450
                    elif c_id == 6 and 3 in carat_ids: c_offset = 950
                    elif c_id == 6 and 5 in carat_ids: c_offset = 500
                    elif c_id == 7 and 5 in carat_ids: c_offset = 1200
                    elif c_id == 7 and 6 in carat_ids: c_offset = 700
                    elif c_id == 8 and 6 in carat_ids: c_offset = 1500

                    var_price = base_price + m_offset + c_offset
                    sku_str = f"SAT-ET-{prod_id}-{m_id}-{c_id}-{sku_idx}"
                    sku_idx += 1
                    cur.execute("""
                        INSERT INTO product_variants (product_id, metal_id, carat_id, sku, price, stock_quantity, is_available)
                        VALUES (%s, %s, %s, %s, %s, 20, TRUE);
                    """, (prod_id, m_id, c_id, sku_str, var_price))

            # 4. CatalogItems
            catalog_id = f"sat-etsy-{prod_id}"
            spec_str = f"14K Gold | {carat_val}ct GIA Certified Lab Diamond | {title}"
            cur.execute("""
                INSERT INTO "CatalogItems" ("Id", "Name", "CategoryId", "Spec", "PriceUSD", "Price", "ImageUrl", "GalleryImages", "MetalOptions", "CaratOptions", "IsActive", "CreatedAt")
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, TRUE, NOW());
            """, (catalog_id, title, str(cat_id), spec_str, base_price, base_price, cloud_url, cloud_url, metal_options_str, carat_options_str))

            conn.commit()
            inserted_count += 1
        except Exception as e:
            conn.rollback()
            print(f"Error saving {lid}: {e}")

    cur.close()
    conn.close()
    print(f"\n========================================================")
    print(f"COMPLETE! Successfully inserted {inserted_count} Etsy rings into SAT Jewel DB!")
    print(f"========================================================")

if __name__ == '__main__':
    main()
