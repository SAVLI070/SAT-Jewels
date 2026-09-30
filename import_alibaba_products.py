import os
import io
import re
import sys
import time
import psycopg2
from PIL import Image
import cloudinary
import cloudinary.uploader
from concurrent.futures import ThreadPoolExecutor, as_completed

cloudinary.config(
  cloud_name = 'ihcs8m6o',
  api_key = '826999397858529',
  api_secret = 'S8guO0Os21rzu4vxKAztT39irto'
)

DB_URL = "postgresql://satjewels_admin:SatJewels%23Db2026%21Secure@satjewels-postgres.c4r4s48oeqi1.us-east-1.rds.amazonaws.com:5432/satjewels_db"
BASE_DIR = os.path.expanduser('~/Downloads/alibaba new 3 photo')

classification = {
    'RG1005-14K-7-1ct': {'shape': 10, 'cat': 1, 'carat': 1.0, 'title': 'Petite Pavé Solitaire Heart Lab Diamond Engagement Ring RG1005'},
    'RG1018-14K-7-3ct': {'shape': 8, 'cat': 1, 'carat': 3.0, 'title': 'Hidden Halo Radiant Cut Lab Diamond Engagement Ring RG1018'},
    'RG1019-14K-4ct-7': {'shape': 2, 'cat': 1, 'carat': 4.0, 'title': 'Pavé Stem Oval Lab Grown Diamond Engagement Ring RG1019'},
    'RG1024-14K-7-1ct': {'shape': 5, 'cat': 1, 'carat': 1.0, 'title': 'Signature Teardrop Pear Cut Diamond Solitaire Ring RG1024'},
    'RG1025-14K-7-1ct': {'shape': 3, 'cat': 1, 'carat': 1.0, 'title': 'Classic Emerald Cut Cathedral Solitaire Engagement Ring RG1025'},
    'RG1025-14K-7-1ct 2': {'shape': 3, 'cat': 1, 'carat': 1.0, 'title': 'Modern Emerald Cut Diamond Solitaire Ring RG1025-II'},
    'RG1034-14K-7-1ct': {'shape': 2, 'cat': 1, 'carat': 1.0, 'title': 'Bezel Floating Oval Cut Lab Diamond Solitaire Ring RG1034'},
    'RG1044-14K-7': {'shape': 7, 'cat': 3, 'carat': 1.5, 'title': "Men's Regal Cushion Pavé Solitaire Wedding Band RG1044"},
    'RG1060-14K-7': {'shape': 8, 'cat': 1, 'carat': 2.0, 'title': 'Luminous Radiant Cut Halo Diamond Engagement Ring RG1060'},
    'RG1082-14K-7': {'shape': 3, 'cat': 1, 'carat': 1.5, 'title': 'East-West Bar Setting Emerald Cut Diamond Ring RG1082'},
    'RG1083-14K-7-1ct': {'shape': 1, 'cat': 1, 'carat': 1.0, 'title': 'Classic Brilliant Round Cut Lab Diamond Solitaire Ring RG1083'},
    'RG1105-14K-7': {'shape': 3, 'cat': 3, 'carat': 1.25, 'title': "Men's Flush Set Emerald Cut Diamond Signet Band RG1105"},
    'RG1121-14K-7-2ct': {'shape': 8, 'cat': 1, 'carat': 2.0, 'title': 'Radiant Cut Floating Solitaire Lab Diamond Ring RG1121'},
    'RG1139-14K-7': {'shape': 1, 'cat': 3, 'carat': 0.75, 'title': "Men's Channel Set Brilliant Round Diamond Wedding Band RG1139"},
    'RG1140-14K-1ct-7': {'shape': 1, 'cat': 1, 'carat': 1.0, 'title': 'Cathedral Round Cut Lab Grown Diamond Engagement Ring RG1140'},
    'RG1144-14K-7': {'shape': 3, 'cat': 3, 'carat': 1.0, 'title': "Men's Luxury Bezel Emerald Cut Gold Wedding Ring RG1144"},
    'RG1147-14K-7-2ct': {'shape': 6, 'cat': 1, 'carat': 2.0, 'title': 'Princess Cut Quad-Prong Diamond Engagement Ring RG1147'},
    'RG1151-14K-7-2ct': {'shape': 3, 'cat': 1, 'carat': 2.0, 'title': 'Four-Prong Emerald Cut Lab Diamond Solitaire Ring RG1151'},
    'RG1159-14K-1ct-7': {'shape': 3, 'cat': 1, 'carat': 1.0, 'title': 'Twisted Rope Emerald Cut Lab Diamond Ring RG1159'},
    'RG1175-14K-7-1ct': {'shape': 8, 'cat': 1, 'carat': 1.0, 'title': 'Radiant Cut Bezel Framed Solitaire Engagement Ring RG1175'},
    'RG1177-14K-7-1ct': {'shape': 2, 'cat': 1, 'carat': 1.0, 'title': 'Oval Cut Four-Prong Wire Basket Solitaire Ring RG1177'},
    'RG1197-14K-7': {'shape': 2, 'cat': 1, 'carat': 1.5, 'title': 'Oval Cut Pavé Hidden Wrap Engagement Ring RG1197'},
    'RG1201-14K-7-1ct': {'shape': 1, 'cat': 1, 'carat': 1.0, 'title': 'Six-Prong Brilliant Round Lab Diamond Solitaire Ring RG1201'},
    'RG1205-14K-7-1ct': {'shape': 4, 'cat': 1, 'carat': 1.0, 'title': 'Marquise Cut Pavé Band Lab Diamond Engagement Ring RG1205'},
    'RG1262-14K-1ct': {'shape': 8, 'cat': 1, 'carat': 1.0, 'title': 'Emerald Cut Floating Beaded Basket Engagement Ring RG1262'},
    'RG1274-14K-7': {'shape': 3, 'cat': 3, 'carat': 1.0, 'title': "Men's Open Grid Emerald Diamond Statement Ring RG1274"},
    'RG1283-14K-7': {'shape': 5, 'cat': 1, 'carat': 1.5, 'title': 'Pear Cut Three-Prong Delicate Engagement Ring RG1283'},
    'RG1321-14K-7': {'shape': 3, 'cat': 1, 'carat': 1.5, 'title': 'Three-Stone Emerald Cut Diamond Trio Ring RG1321'},
    'RG1322-14K-7': {'shape': 1, 'cat': 2, 'carat': 0.75, 'title': 'Channel Set Round Diamond Eternity Wedding Band RG1322'},
    'RG1323-14K-7': {'shape': 1, 'cat': 2, 'carat': 0.5, 'title': 'Contoured Chevron Diamond Wedding Band RG1323'},
    'RG1324-14K-1ct-7': {'shape': 8, 'cat': 1, 'carat': 1.0, 'title': 'Radiant Cut Cluster Botanical Accent Diamond Ring RG1324'},
    'RG1325-14K-1ct-7': {'shape': 7, 'cat': 1, 'carat': 1.0, 'title': 'Cushion Cut Micro-Pavé Halo Diamond Engagement Ring RG1325'},
    'RG1328-14K-1ct-7': {'shape': 2, 'cat': 1, 'carat': 1.0, 'title': 'Slender Shank Oval Cut Solitaire Engagement Ring RG1328'},
    'RG1334-14K-7': {'shape': 1, 'cat': 3, 'carat': 0.25, 'title': "Men's Brushed Gold Inner Halo Wedding Band RG1334"},
    'RG1344-14K-1ct-7': {'shape': 1, 'cat': 2, 'carat': 1.0, 'title': 'Flat-Edge Flush Set Round Diamond Modern Band RG1344'},
    'RG1350-14K-1ct-7': {'shape': 10, 'cat': 1, 'carat': 1.0, 'title': 'Twisted Shank Heart Shaped Lab Diamond Ring RG1350'},
    'RG1354-14K-1ct-7': {'shape': 1, 'cat': 1, 'carat': 1.0, 'title': 'Braided Vine Round Brilliant Solitaire Engagement Ring RG1354'},
    'RG1355-14K-1ct-7': {'shape': 8, 'cat': 1, 'carat': 1.0, 'title': 'Twisted Pavé Radiant Cut Diamond Engagement Ring RG1355'},
    'RG1357-14K-1ct-7': {'shape': 5, 'cat': 1, 'carat': 1.0, 'title': 'Twisted Vine Pear Cut Lab Diamond Solitaire Ring RG1357'},
    'RG1362-14K-7': {'shape': 2, 'cat': 1, 'carat': 1.0, 'title': 'East-West Twisted Oval Solitaire Engagement Ring RG1362'},
    'RG1363-14K-7': {'shape': 1, 'cat': 1, 'carat': 1.0, 'title': 'Entwined Shank Brilliant Round Lab Diamond Ring RG1363'},
    'RG1365-14K-7': {'shape': 5, 'cat': 1, 'carat': 1.5, 'title': 'Toi et Moi Pear & Marquise Dual Stone Diamond Ring RG1365'},
    'RG1370-14K-0.5ct-7': {'shape': 1, 'cat': 1, 'carat': 0.5, 'title': 'Delicate Petite Round Lab Diamond Solitaire Ring RG1370'},
    'RG1372-14K-0.5ct-7': {'shape': 1, 'cat': 1, 'carat': 0.5, 'title': 'Spiral Bypass Round Diamond Engagement Ring RG1372'},
    'RG1373-14K-0.5ct-7': {'shape': 4, 'cat': 1, 'carat': 0.5, 'title': 'Dainty Marquise Cut Solitaire Engagement Ring RG1373'},
    'RG1375-14K-0.5ct-7': {'shape': 5, 'cat': 1, 'carat': 0.5, 'title': 'Floating Pear Cut Split Shank Diamond Ring RG1375'},
    'RG1376-14K-0.5ct-7': {'shape': 5, 'cat': 1, 'carat': 0.5, 'title': 'Tiara V-Crown Pear Cut Diamond Engagement Ring RG1376'},
    'RG1377-14K-0.5ct-7': {'shape': 3, 'cat': 3, 'carat': 0.5, 'title': "Men's Flush Set Emerald Cut Signet Ring RG1377"},
    'RG1380-14K-1ct-7': {'shape': 5, 'cat': 1, 'carat': 1.0, 'title': 'Botanical Leaf Pear Cut Lab Diamond Engagement Ring RG1380'},
    'RG1382-14K-1.5ct-7': {'shape': 2, 'cat': 1, 'carat': 1.5, 'title': 'Vintage Floral Branch Oval Cut Diamond Ring RG1382'},
    'RG1384-14K-1ct-7': {'shape': 1, 'cat': 1, 'carat': 1.0, 'title': 'Vintage Leaf Vine Round Brilliant Diamond Ring RG1384'},
    'RG1385-14K-1ct-7': {'shape': 10, 'cat': 1, 'carat': 1.0, 'title': 'Romantic Botanical Heart Shaped Diamond Ring RG1385'},
    'RG1390-14K-1ct-7': {'shape': 8, 'cat': 1, 'carat': 1.0, 'title': 'Forest Flora Radiant Cut Lab Diamond Ring RG1390'}
}

def resize_and_upload(file_path, folder_id, idx):
    try:
        im = Image.open(file_path).convert('RGB')
        im.thumbnail((1920, 1920), Image.Resampling.LANCZOS)
        buf = io.BytesIO()
        im.save(buf, format='JPEG', quality=88, optimize=True)
        buf.seek(0)
        
        sku_clean = re.sub(r'[^a-zA-Z0-9_\-]', '_', folder_id)
        pub_id = f"{sku_clean}_{idx}_{int(time.time()*1000)%100000}"
        res = cloudinary.uploader.upload(
            buf,
            folder=f"sat_jewels/catalog/alibaba/{sku_clean}",
            public_id=pub_id,
            overwrite=True
        )
        return (idx, res.get('secure_url'))
    except Exception as e:
        print(f"Error uploading {file_path}: {e}")
        return (idx, None)

def process_all():
    conn = psycopg2.connect(DB_URL)
    conn.autocommit = False
    cur = conn.cursor()
    
    # Sync DB sequences first
    cur.execute("""
        DO $$
        BEGIN
            BEGIN PERFORM setval(pg_get_serial_sequence('products', 'id'), COALESCE((SELECT MAX(id) FROM products), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
            BEGIN PERFORM setval(pg_get_serial_sequence('product_variants', 'id'), COALESCE((SELECT MAX(id) FROM product_variants), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
            BEGIN PERFORM setval(pg_get_serial_sequence('product_images', 'id'), COALESCE((SELECT MAX(id) FROM product_images), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
        END $$;
    """)
    conn.commit()

    dirs = sorted([d for d in os.listdir(BASE_DIR) if os.path.isdir(os.path.join(BASE_DIR, d))])
    print(f"Total directories to process: {len(dirs)}")

    # Carat weight price and options mapping
    carat_options_map = {
        0.25: (850, [1, 2], ["0.50 CT"]),
        0.5: (950, [1, 2, 3], ["0.50 CT", "0.75 CT (+150 USD)", "1.00 CT (+350 USD)"]),
        0.75: (1150, [1, 2, 3], ["0.50 CT (-150 USD)", "0.75 CT", "1.00 CT (+200 USD)"]),
        1.0: (1350, [3, 4, 5, 6], ["1.00 CT", "1.25 CT (+200 USD)", "1.50 CT (+450 USD)", "2.00 CT (+950 USD)"]),
        1.25: (1550, [3, 4, 5], ["1.00 CT (-200 USD)", "1.25 CT", "1.50 CT (+250 USD)"]),
        1.5: (1750, [4, 5, 6, 7], ["1.25 CT (-250 USD)", "1.50 CT", "2.00 CT (+500 USD)", "3.00 CT (+1400 USD)"]),
        2.0: (2250, [5, 6, 7], ["1.50 CT (-500 USD)", "2.00 CT", "3.00 CT (+900 USD)"]),
        3.0: (3150, [6, 7, 8], ["2.00 CT (-900 USD)", "3.00 CT", "4.00 CT (+1200 USD)"]),
        4.0: (4200, [7, 8, 9], ["3.00 CT (-1000 USD)", "4.00 CT", "5.00 CT (+1400 USD)"])
    }

    # Metals: 14K Yellow Gold (4), 14K White Gold (5), 14K Rose Gold (6), 18K Yellow Gold (7), 18K White Gold (8), 950 Platinum (10)
    configured_metals = [
        (4, "14K Yellow Gold", 0),
        (5, "14K White Gold", 0),
        (6, "14K Rose Gold", 0),
        (7, "18K Yellow Gold", 300),
        (8, "18K White Gold", 300),
        (10, "950 Platinum", 650)
    ]
    metal_options_str = "14K Yellow Gold|14K White Gold|14K Rose Gold|18K Yellow Gold (+300 USD)|18K White Gold (+300 USD)|950 Platinum (+650 USD)"

    inserted_count = 0

    for d_idx, d in enumerate(dirs, 1):
        info = classification.get(d)
        if not info:
            print(f"Skipping unmapped directory: {d}")
            continue

        p_dir = os.path.join(BASE_DIR, d)
        raw_files = sorted([f for f in os.listdir(p_dir) if f.lower().endswith(('.jpg', '.jpeg', '.png')) and not f.startswith('.')])
        if not raw_files:
            print(f"No image files in {d}")
            continue

        print(f"\n[{d_idx}/{len(dirs)}] Processing {d} ({len(raw_files)} images)...")

        # Upload images concurrently
        uploaded_results = []
        with ThreadPoolExecutor(max_workers=6) as executor:
            future_to_idx = {
                executor.submit(resize_and_upload, os.path.join(p_dir, f), d, i): i 
                for i, f in enumerate(raw_files)
            }
            for future in as_completed(future_to_idx):
                res = future.result()
                if res[1]:
                    uploaded_results.append(res)

        uploaded_results.sort(key=lambda x: x[0])
        image_urls = [u for idx, u in uploaded_results]

        if not image_urls:
            print(f"Failed to upload any images for {d}, skipping.")
            continue

        main_img = image_urls[0]
        gallery_str = ",".join(image_urls)

        # Determine price & carats
        carat_val = info.get('carat', 1.0)
        c_pricing = carat_options_map.get(carat_val, (1350, [3, 5, 6], ["1.00 CT", "1.50 CT (+450 USD)", "2.00 CT (+950 USD)"]))
        base_price = c_pricing[0]
        carat_ids = c_pricing[1]
        carat_options_str = "|".join(c_pricing[2])

        # Clean unique slug
        clean_slug = re.sub(r'[^a-z0-9\-]', '-', info['title'].lower()).strip('-')
        clean_slug = re.sub(r'\-+', '-', clean_slug)[:55]
        unique_slug = f"{clean_slug}-{d.split('-')[0].lower()}"

        try:
            # 1. Insert into products
            cur.execute("""
                INSERT INTO products (title, slug, price, category_id, diamond_shape_id, created_at, image_path)
                VALUES (%s, %s, %s, %s, %s, NOW(), %s)
                RETURNING id;
            """, (info['title'], unique_slug, base_price, info['cat'], info['shape'], main_img))
            prod_id = cur.fetchone()[0]

            # 2. Insert into product_images
            for order, img_url in enumerate(image_urls, 1):
                cur.execute("""
                    INSERT INTO product_images (product_id, image_path, display_order)
                    VALUES (%s, %s, %s);
                """, (prod_id, img_url, order))

            # 3. Insert into product_variants (Specific configured metals x carats)
            sku_idx = 100
            for m_id, m_name, m_offset in configured_metals:
                for c_id in carat_ids:
                    c_offset = 0
                    if c_id == 1 and carat_val != 0.5: c_offset = -150
                    elif c_id == 2 and carat_val != 0.75: c_offset = -100
                    elif c_id == 5 and carat_val == 1.0: c_offset = 450
                    elif c_id == 6 and carat_val == 1.0: c_offset = 950
                    elif c_id == 7 and carat_val == 2.0: c_offset = 900
                    elif c_id == 8 and carat_val == 3.0: c_offset = 1200
                    var_price = base_price + m_offset + c_offset
                    sku_str = f"SAT-{prod_id}-{m_id}-{c_id}-{sku_idx}"
                    sku_idx += 1
                    cur.execute("""
                        INSERT INTO product_variants (product_id, metal_id, carat_id, sku, price, stock_quantity, is_available)
                        VALUES (%s, %s, %s, %s, %s, 25, TRUE);
                    """, (prod_id, m_id, c_id, sku_str, var_price))

            # 4. Insert into CatalogItems
            catalog_id = f"sat-prod-{prod_id}"
            spec_str = f"14K Gold | {carat_val}ct GIA Certified Lab Diamond | {info['title']}"
            cur.execute("""
                INSERT INTO "CatalogItems" ("Id", "Name", "CategoryId", "Spec", "PriceUSD", "Price", "ImageUrl", "GalleryImages", "MetalOptions", "CaratOptions", "IsActive", "CreatedAt")
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, TRUE, NOW());
            """, (catalog_id, info['title'], str(info['cat']), spec_str, base_price, base_price, main_img, gallery_str, metal_options_str, carat_options_str))

            conn.commit()
            inserted_count += 1
            print(f"-> SUCCESS: Inserted product {prod_id} ('{info['title']}') with {len(image_urls)} images and variants.")
        except Exception as e:
            conn.rollback()
            print(f"-> FAILED inserting {d}: {e}")

    cur.close()
    conn.close()
    print(f"\n==========================================")
    print(f"COMPLETED: Successfully imported {inserted_count} products into RDS PostgreSQL!")
    print(f"==========================================")

if __name__ == '__main__':
    process_all()
