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
BASE_DIR = os.path.expanduser('~/Downloads/ali photo edites 1')

# Custom luxury titles & pricing for each watch model folder
watch_titles = {
    'A-LB(JAMBO)-FB-E-113': ('Audemars Piguet Royal Oak Jumbo Full Baguette Bezel Timepiece A-113', 4450),
    'A-LB-JAMBO-FW-R-122': ('Audemars Piguet Royal Oak Jumbo Frosted White Edition A-122', 3850),
    'A-LB-JAMBO-FW-R-122 2': ('Audemars Piguet Royal Oak Jumbo Frosted Diamond Edition A-122-II', 3950),
    'A-LB-JAMBO-TTR-N-125': ('Audemars Piguet Royal Oak Jumbo Two-Tone Rose Gold A-125', 4150),
    'A-LB-R-E-103': ('Audemars Piguet Royal Oak Diamond Baguette Bezel Chrono A-103', 4250),
    'A-OK-WR-N-139': ('Audemars Piguet Royal Oak Offshore White Ceramic Chronograph A-139', 4650),
    'A-R-FW-R-162': ('Audemars Piguet Royal Oak Frosted White Gold Pavé Dial A-162', 4350),
    'A-R-RW-RB-R-160': ('Audemars Piguet Royal Oak Rainbow Gem-Set Bezel Luxury Watch A-160', 4850),
    'A-RA(JUMBO)-WR-N-132': ('Audemars Piguet Royal Oak Jumbo White Textured Dial A-132', 3750),
    'A-ST(JAMBO)-TTY-N-112': ('Audemars Piguet Royal Oak Jumbo Two-Tone Yellow Gold A-112', 3950),
    'A-ST-FW-B-124': ('Audemars Piguet Royal Oak Steel Black Grande Tapisserie Dial A-124', 3650),
    'A-ST-FW-E-115': ('Audemars Piguet Royal Oak Steel Frosted Emerald Bezel A-115', 4200),
    'A-ST-FW-SK-127': ('Audemars Piguet Royal Oak Double Balance Wheel Skeleton A-127', 4750),
    'A-ST-JAMBO-FW-N-153': ('Audemars Piguet Royal Oak Jumbo Ultra-Thin Silver Dial A-153', 3800),
    'A-ST-TTB-CUSTOM-142': ('Audemars Piguet Royal Oak Two-Tone Bronze Dial Custom A-142', 3950),
    'A-ST-TTR-E-118': ('Audemars Piguet Royal Oak Steel Two-Tone Rose Bezel A-118', 4100),
    'AP-Lthr-Green-175': ('Audemars Piguet Royal Oak Chronograph British Racing Green Leather AP-175', 3850),
    'AP-ST-FW-Green Bez-180': ('Audemars Piguet Royal Oak Steel Olive Green Ceramic Bezel AP-180', 3950),
    'AP-ST-FW-OG-Dial-178': ('Audemars Piguet Royal Oak Classic Steel Bleu Nuit Tapisserie AP-178', 3700),
    'Ap-ST-FW-Arabic Custom-177': ('Audemars Piguet Royal Oak Custom Eastern Arabic Numeral Dial AP-177', 4150),
    'C-CO-RW-R-138': ('Cartier Santos De Cartier Two-Tone Rose Gold Chronograph C-138', 3600),
    'C-LB-RW-SK-130': ('Cartier Santos Skeleton Noctambule Rose Gold Mechanical C-130', 4450),
    'C-ST(C)-TTR-R-119': ('Cartier Santos Classic Two-Tone Roman Numerals Luxury Watch C-119', 3250),
    'C-ST(POP)-AR(RED)-136': ('Cartier Santos Custom Burgundy Red Roman Dial Timepiece C-136', 3450),
    'C-ST(RED DIAL)-FW-R-114': ('Cartier Santos De Cartier Crimson Sunray Red Dial Edition C-114', 3550),
    'C-ST(TH)-WR-R-134': ('Cartier Santos-Dumont Extra-Flat Stainless Steel Silver Dial C-134', 2950),
    'C-ST-(BW)-R-135': ('Cartier Santos Black & White Contrast Roman Numeral Dial C-135', 3150),
    'C-ST-FP-174': ('Cartier Santos Full Pavé Diamond Case & Bracelet Luxury Watch C-174', 4950),
    'C-ST-FW-Bg-mid-164': ('Cartier Santos Diamond Baguette Bezel Midsize Steel C-164', 3850),
    'C-ST-FW-Pink Arabic-171': ('Cartier Santos Custom Sakura Pink Eastern Arabic Numeral Dial C-171', 3650),
    'C-ST-FW-R(BL)-131': ('Cartier Santos Gradient Deep Blue Sunburst Dial Edition C-131', 3400),
    'C-ST-FW-R(G)-129': ('Cartier Santos Sunburst Emerald Green Dial Stainless Steel C-129', 3450),
    'C-ST-FW-R-117': ('Cartier Santos Classic Silver Opaline Roman Dial Steel C-117', 2900),
    'C-ST-FW-R-121': ('Cartier Santos Diamond Set Bezel Silver Dial Automatic C-121', 3650),
    'C-ST-FW-R-145': ('Cartier Santos Frosted Diamond Bezel Opaline Dial C-145', 3750),
    'C-ST-FW-R-148': ('Cartier Santos Steel Bezel Brushed Anthracite Dial C-148', 3200),
    'C-ST-FY-R-150': ('Cartier Santos Two-Tone 18K Yellow Gold Bezel Roman Dial C-150', 3500),
    'C-ST-RW-36-167': ('Cartier Santos-Dumont 36mm Rose Gold & Steel Elegant Timepiece C-167', 3100),
    'C-ST-RW-R-Throns-157': ('Cartier Santos Thorn Skeletonized Mechanical Art Timepiece C-157', 4600),
    'C-ST-TTR-R-155': ('Cartier Santos Two-Tone Rose Gold Automatic Bracelet Watch C-155', 3550),
    'C-ST-YW-COLET-169': ('Cartier Santos Yellow Gold Colet Diamond Bezel Edition C-169', 3900),
    'C-STP-FW-R-144': ('Cartier Santos Prestige Polished Steel Diamond Marker Watch C-144', 3350),
    'C-lb-w-r-101': ('Cartier Ballon Bleu Diamond Bezel Guilloché Dial Timepiece C-101', 3600),
    'C-st-b-sk-102': ('Cartier Santos Skeleton Architectural Blue Steel Mechanical C-102', 4500),
    'Custom-FW-1-166': ('Atelier Bespoke Diamond Bezel Custom Chronograph Timepiece FW-166', 4250)
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
            base_price = 3450

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
            # Metal variants: 5 (Steel/White Gold), 7 (Yellow Gold), 9 (Rose Gold)
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
    print(f"COMPLETED: Successfully imported {inserted_count} Luxury Watches into database!")
    print("==========================================")

if __name__ == '__main__':
    process_all_watches()
