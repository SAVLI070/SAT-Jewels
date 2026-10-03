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
BASE_DIR = os.path.expanduser('~/Downloads/ali photo edites')

watch_titles = {
    'Custom-green-165': ('Atelier Bespoke Emerald Green Dial Custom Timepiece 165', 4450),
    'P-LB-FR-E(BALACK)-109': ('Patek Philippe Nautilus Black Dial Frosted Rose Gold P-109', 5200),
    'P-LB-FW-E-116': ('Patek Philippe Nautilus Frosted White Gold Diamond Bezel P-116', 5400),
    'P-ST-FW(OB)-E-111': ('Patek Philippe Aquanaut Steel Blue Embossed Dial P-111', 4850),
    'P-ST-FW-170': ('Patek Philippe Nautilus Classic Stainless Steel Silver Dial P-170', 4650),
    'P-ST-FW-E(B)-107': ('Patek Philippe Nautilus Baguette Diamond Bezel Blue Dial P-107', 5350),
    'P-ST-FY-E-149': ('Patek Philippe Nautilus Yellow Gold Bezel Slate Dial P-149', 4950),
    'P-ST-RW-Stick-159': ('Patek Philippe Aquanaut Rose Gold Stick Markers Watch P-159', 4800),
    'P-ST-TTR-E-152': ('Patek Philippe Nautilus Two-Tone Rose Gold Diamond Bezel P-152', 5100),
    'P-ST-TTY-E-110': ('Patek Philippe Nautilus Two-Tone Yellow Gold Pavé Dial P-110', 5150),
    'PP-ST-FW-E-128': ('Patek Philippe Grand Complications Steel Diamond Bezel PP-128', 5500),
    'PP-ST-FW-E-133': ('Patek Philippe Calatrava Diamond Bezel Guilloché Dial PP-133', 4750),
    'R-J-FW-R-141': ('Rolex Datejust Jubilee Bracelet Frosted White Gold Roman Dial R-141', 3950),
    'R-JB-FW-AB-151': ('Rolex Datejust Jubilee Fluted Bezel Arabic Numeral Dial R-151', 4150),
    'R-JB-FW-R-143': ('Rolex Datejust Jubilee White Gold Diamond Hour Markers R-143', 4050),
    'R-JB-TTR-R-154': ('Rolex Datejust Two-Tone Everose Gold Jubilee Roman Dial R-154', 4250),
    'R-OB-FW-AB-123': ('Rolex Oyster Perpetual White Gold Arabic Numeral Dial R-123', 3850),
    'R-OB-TTR-E-146': ('Rolex Submariner Two-Tone Rose Gold Emerald Green Bezel R-146', 4400),
    'R-OB-TTY-E-147': ('Rolex Submariner Two-Tone Yellow Gold Diamond Bezel R-147', 4450),
    'R-OS-FE-R-140': ('Rolex Oyster Perpetual Frosted Emerald Gem-Set Bezel R-140', 4550),
    'R-OS-FW-E(B)-108': ('Rolex Cosmograph Daytona Baguette Diamond Bezel Blue Dial R-108', 5600),
    'R-OS-FW-E-126': ('Rolex Cosmograph Daytona Frosted White Gold Chronograph R-126', 5450),
    'R-PS-FW-105': ('Rolex Day-Date President Steel Silver Sunray Dial R-105', 4100),
    'R-PS-FW-R-106': ('Rolex Day-Date President Diamond Roman Numeral Dial R-106', 4350),
    'R-PT-FW-E-120': ('Rolex Daytona Platinum Ice Blue Dial Diamond Baguettes R-120', 5850),
    'R-S-W-E-104': ('Rolex Sky-Dweller Annual Calendar White Gold Diamond Bezel R-104', 5250),
    'R-ST-FR-Custom173': ('Rolex Custom Frosted Rose Gold Atelier Edition R-173', 4650),
    'R-ST-FullGreen-163': ('Rolex Submariner Full Emerald Green Ceramic Bezel & Dial R-163', 4350),
    'R-ST-JUb-EM-MS-168': ('Rolex Datejust Jubilee Emerald Cut Bezel Meteorite Dial R-168', 4950),
    'R-ST-PW-172': ('Rolex Day-Date President Pearl White Dial Luxury Timepiece R-172', 4200),
    'R-ST-RW-R-Snake-158': ('Rolex Custom Serpenti Engraved Rose Gold Luxury Watch R-158', 5100),
    'R-ST-Sky-YW-Custom-176': ('Rolex Sky-Dweller Custom Yellow Gold Sunburst Champagne Dial R-176', 4850),
    'R-St-YW-Emrald-Skull-161': ('Rolex Custom Yellow Gold Emerald Eyes Skull Atelier R-161', 5300),
    'RM-FW-yellow-179': ('Richard Mille RM-011 Tourbillon Yellow Ceramic Skeleton RM-179', 6800),
    'RM-LB-FW-156': ('Richard Mille RM-055 Carbon Skeletonized White Gold RM-156', 6500)
}

def resize_and_upload(file_path, folder_id, idx):
    try:
        im = Image.open(file_path).convert('RGB')
        im.thumbnail((1600, 1600), Image.Resampling.LANCZOS)
        buf = io.BytesIO()
        im.save(buf, format='JPEG', quality=86, optimize=True)
        buf.seek(0)
        
        sku_clean = re.sub(r'[^a-zA-Z0-9_\-]', '_', folder_id)
        pub_id = f"watch_{sku_clean}_{idx}_{int(time.time()*1000)%100000}"
        res = cloudinary.uploader.upload(
            buf,
            folder=f"sat_jewels/catalog/luxury_watches/{sku_clean}",
            public_id=pub_id,
            overwrite=True
        )
        return (idx, res.get('secure_url'))
    except Exception as e:
        print(f"Error uploading {file_path}: {e}")
        return (idx, None)

def process_all_watches():
    conn = psycopg2.connect(DB_URL)
    conn.autocommit = False
    cur = conn.cursor()
    
    # Ensure sequence synchronization
    cur.execute("""
        DO $$
        BEGIN
            BEGIN PERFORM setval(pg_get_serial_sequence('products', 'id'), COALESCE((SELECT MAX(id) FROM products), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
            BEGIN PERFORM setval(pg_get_serial_sequence('product_variants', 'id'), COALESCE((SELECT MAX(id) FROM product_variants), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
            BEGIN PERFORM setval(pg_get_serial_sequence('product_images', 'id'), COALESCE((SELECT MAX(id) FROM product_images), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
        END $$;
    """)
    conn.commit()

    dirs = sorted([d for d in os.listdir(BASE_DIR) if os.path.isdir(os.path.join(BASE_DIR, d)) and not d.startswith('.')])
    print(f"Total watch directories to process: {len(dirs)}")

    metal_options_str = "316L Stainless Steel|18K Rose Gold Finish (+350 USD)|18K Yellow Gold Finish (+350 USD)|Titanium DLC (+250 USD)"
    size_options_str = "40mm Case|41mm Case (+150 USD)|42mm Case (+250 USD)"
    spec_str = "Swiss Automatic Mechanical Movement | Scratch-Resistant Sapphire Crystal | 50m Water Resistance | Insured Armored Dispatch"

    inserted_count = 0

    for d_idx, d in enumerate(dirs, 1):
        p_dir = os.path.join(BASE_DIR, d)
        raw_files = sorted([f for f in os.listdir(p_dir) if f.lower().endswith(('.jpg', '.jpeg', '.png')) and not f.startswith('.')])
        if not raw_files:
            print(f"No image files in {d}, skipping.")
            continue

        title_info = watch_titles.get(d)
        if title_info:
            title, base_price = title_info
        else:
            clean_name = d.replace('-', ' ').replace('_', ' ').title()
            title = f"Bespoke Luxury Watch {clean_name}"
            base_price = 3950

        print(f"\n[{d_idx}/{len(dirs)}] Uploading {d} ({len(raw_files)} images)...")

        # Concurrently resize & upload images to Cloudinary
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
            print(f"Failed to upload images for {d}, skipping.")
            continue

        main_img = image_urls[0]
        gallery_str = ",".join(image_urls)

        # Unique clean slug
        clean_slug = re.sub(r'[^a-z0-9\-]', '-', title.lower()).strip('-')
        clean_slug = re.sub(r'\-+', '-', clean_slug)[:55]
        unique_slug = f"{clean_slug}-{d.split('-')[0].lower()}-{d_idx}"

        try:
            # 1. Insert into products (Category 9 = Luxury Watch, DiamondShape 11 = Other/None)
            cur.execute("""
                INSERT INTO products (title, slug, price, category_id, diamond_shape_id, created_at, image_path, is_active)
                VALUES (%s, %s, %s, 9, 11, NOW(), %s, TRUE)
                RETURNING id;
            """, (title, unique_slug, base_price, main_img))
            prod_id = cur.fetchone()[0]

            # 2. Insert into product_images
            for order, img_url in enumerate(image_urls, 1):
                cur.execute("""
                    INSERT INTO product_images (product_id, image_path, display_order)
                    VALUES (%s, %s, %s);
                """, (prod_id, img_url, order))

            # 3. Insert into product_variants
            configured_metals = [(5, 0), (7, 350), (9, 350)]
            carat_sizes = [(3, 0), (5, 150)] # 40mm, 42mm
            v_sku_idx = 1
            for m_id, m_offset in configured_metals:
                for c_id, c_offset in carat_sizes:
                    v_price = base_price + m_offset + c_offset
                    v_sku = f"SAT-WATCH-{prod_id}-{m_id}-{c_id}"
                    cur.execute("""
                        INSERT INTO product_variants (product_id, metal_id, carat_id, sku, price, stock_quantity, is_available)
                        VALUES (%s, %s, %s, %s, %s, 15, TRUE);
                    """, (prod_id, m_id, c_id, v_sku, v_price))
                    v_sku_idx += 1

            # 4. Insert into CatalogItems
            catalog_id = f"sat-prod-{prod_id}"
            cur.execute("""
                INSERT INTO "CatalogItems" ("Id", "Name", "CategoryId", "Spec", "PriceUSD", "Price", "ImageUrl", "GalleryImages", "MetalOptions", "CaratOptions", "IsActive", "CreatedAt")
                VALUES (%s, %s, '9', %s, %s, %s, %s, %s, %s, %s, TRUE, NOW());
            """, (catalog_id, title, spec_str, base_price, base_price, main_img, gallery_str, metal_options_str, size_options_str))

            conn.commit()
            inserted_count += 1
            print(f"-> SUCCESS ({inserted_count}): Inserted product {prod_id} ('{title}') with {len(image_urls)} images.")
        except Exception as e:
            conn.rollback()
            print(f"-> FAILED inserting {d}: {e}")

    cur.close()
    conn.close()
    print("\n==========================================")
    print(f"COMPLETED: Successfully imported {inserted_count} Luxury Watches from 'ali photo edites' into database!")
    print("==========================================")

if __name__ == '__main__':
    process_all_watches()
