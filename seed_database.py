#!/usr/bin/env python3
"""
=============================================================================
SAT JEWEL - AUTOMATED DATABASE SEEDER SCRIPT
=============================================================================
This script:
1. Clears / resets all existing records in `categories`, `diamond_shapes`, 
   `products`, and `product_images` tables to avoid duplicate entries.
2. Creates clean SQL tables (`schema.sql`).
3. Seeds the 6 Main Categories and 11 Diamond Shapes.
4. Scans `/Users/sahil/Desktop/Image/<Category>/<Shape>/<ProductTitle>/`
5. Ingests all 2,800+ Products and 21,000+ Product Images with proper 
   `category_id`, `diamond_shape_id`, `image_path`, and `display_order`.
=============================================================================
"""

import os
import re
import sys
import shutil
import psycopg2
from psycopg2.extras import execute_values

# Master Configuration
DESKTOP_IMAGE_PATH = "/Users/sahil/Desktop/Image"
WWWROOT_PRODUCTS_PATH = "/Users/sahil/Desktop/SAT1/wwwroot/assets/products"

DEFAULT_CONN_STRING = (
    "host=ep-soft-sound-azkeypgg-pooler.c-3.ap-southeast-1.aws.neon.tech "
    "port=5432 dbname=neondb user=neondb_owner password=npg_yX8TV4rmHEqR sslmode=require"
)

# 1. Master Categories Mapping
CATEGORIES = [
    {"name": "Engagement Rings", "slug": "engagement-rings"},
    {"name": "Wedding Rings", "slug": "wedding-rings"},
    {"name": "Men's Wedding Bands", "slug": "mens-wedding-bands"},
    {"name": "Earrings", "slug": "earrings"},
    {"name": "Bracelets", "slug": "bracelets"},
    {"name": "Necklaces", "slug": "necklaces"}
]

# 2. Master Diamond Shapes Mapping
DIAMOND_SHAPES = [
    {"name": "Round", "slug": "round", "icon_url": "/assets/shapes/round.svg"},
    {"name": "Oval", "slug": "oval", "icon_url": "/assets/shapes/oval.svg"},
    {"name": "Emerald", "slug": "emerald", "icon_url": "/assets/shapes/emerald.svg"},
    {"name": "Marquise", "slug": "marquise", "icon_url": "/assets/shapes/marquise.svg"},
    {"name": "Pear", "slug": "pear", "icon_url": "/assets/shapes/pear.svg"},
    {"name": "Princess", "slug": "princess", "icon_url": "/assets/shapes/princess.svg"},
    {"name": "Cushion", "slug": "cushion", "icon_url": "/assets/shapes/cushion.svg"},
    {"name": "Radiant", "slug": "radiant", "icon_url": "/assets/shapes/radiant.svg"},
    {"name": "Asscher", "slug": "asscher", "icon_url": "/assets/shapes/asscher.svg"},
    {"name": "Heart", "slug": "heart", "icon_url": "/assets/shapes/heart.svg"},
    {"name": "Other Shapes", "slug": "other-shapes", "icon_url": "/assets/shapes/other.svg"}
]


def slugify(text):
    """Converts a title into a URL-friendly slug."""
    text = text.lower().strip()
    text = re.sub(r'[^\w\s-]', '', text)
    text = re.sub(r'[\s_-]+', '-', text)
    return text.strip('-')


def calculate_price(title, category_name):
    """Extracts carat weight or calculates a realistic luxury diamond price."""
    carat_match = re.search(r'([\d.]+)\s*CT', title, re.IGNORECASE)
    carat = float(carat_match.group(1)) if carat_match else 1.0
    
    base_price = 1200.00
    if "Wedding" in category_name:
        base_price = 850.00
    elif "Earrings" in category_name:
        base_price = 950.00
    elif "Necklace" in category_name or "Bracelet" in category_name:
        base_price = 1850.00
        
    calculated = base_price + (carat * 1150.00)
    return round(calculated, 2)


def get_db_connection():
    """Resolves connection string from ENV or default Neon Postgres DB."""
    conn_str = os.environ.get("DATABASE_URL", DEFAULT_CONN_STRING)
    if conn_str.startswith("postgres://") or conn_str.startswith("postgresql://"):
        # Convert URI format to psycopg2 key=value format if needed
        import urllib.parse
        url = urllib.parse.urlparse(conn_str)
        conn_str = f"host={url.hostname} port={url.port or 5432} dbname={url.path.lstrip('/')} user={url.username} password={url.password} sslmode=require"
        
    print("Connecting to database...")
    return psycopg2.connect(conn_str)


def reset_schema_and_tables(cur):
    """Truncates/Resets all tables for a clean seeding run."""
    print("🚨 MASTER INSTRUCTION: Truncating/clearing all existing data from tables...")
    cur.execute("""
        DROP TABLE IF EXISTS product_images CASCADE;
        DROP TABLE IF EXISTS "ProductImages" CASCADE;
        DROP TABLE IF EXISTS products CASCADE;
        DROP TABLE IF EXISTS "Products" CASCADE;
        DROP TABLE IF EXISTS diamond_shapes CASCADE;
        DROP TABLE IF EXISTS "DiamondShapes" CASCADE;
        DROP TABLE IF EXISTS categories CASCADE;
        DROP TABLE IF EXISTS "Categories" CASCADE;
    """)

    cur.execute("""
        CREATE TABLE categories (
            id BIGSERIAL PRIMARY KEY,
            name VARCHAR(100) NOT NULL UNIQUE,
            slug VARCHAR(150) NOT NULL UNIQUE
        );

        CREATE TABLE diamond_shapes (
            id BIGSERIAL PRIMARY KEY,
            name VARCHAR(100) NOT NULL UNIQUE,
            slug VARCHAR(150) NOT NULL UNIQUE,
            icon_url VARCHAR(500) DEFAULT ''
        );

        CREATE TABLE products (
            id BIGSERIAL PRIMARY KEY,
            title VARCHAR(255) NOT NULL,
            slug VARCHAR(255) NOT NULL,
            price NUMERIC(18, 2) NOT NULL DEFAULT 0.00,
            category_id BIGINT NOT NULL REFERENCES categories(id) ON DELETE CASCADE,
            diamond_shape_id BIGINT NOT NULL REFERENCES diamond_shapes(id) ON DELETE CASCADE,
            created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
        );

        CREATE INDEX idx_products_category ON products(category_id);
        CREATE INDEX idx_products_diamond_shape ON products(diamond_shape_id);
        CREATE INDEX idx_products_cat_shape ON products(category_id, diamond_shape_id);

        CREATE TABLE product_images (
            id BIGSERIAL PRIMARY KEY,
            product_id BIGINT NOT NULL REFERENCES products(id) ON DELETE CASCADE,
            image_path VARCHAR(500) NOT NULL,
            display_order INT NOT NULL DEFAULT 1
        );

        CREATE INDEX idx_product_images_product ON product_images(product_id);
    """)
    print("Schema reset and tables re-created clean!")


def seed_categories(cur):
    """Seeds the 6 Main Categories and returns a name -> id mapping."""
    print("Seeding 6 Main Categories...")
    category_id_map = {}
    for cat in CATEGORIES:
        cur.execute(
            "INSERT INTO categories (name, slug) VALUES (%s, %s) RETURNING id;",
            (cat["name"], cat["slug"])
        )
        cat_id = cur.fetchone()[0]
        category_id_map[cat["name"].lower()] = cat_id
        print(f"  [Category ID {cat_id}] {cat['name']} ({cat['slug']})")
    return category_id_map


def seed_diamond_shapes(cur):
    """Seeds the 11 Diamond Shapes and returns a name -> id mapping."""
    print("Seeding 11 Diamond Shapes...")
    shape_id_map = {}
    for shape in DIAMOND_SHAPES:
        cur.execute(
            "INSERT INTO diamond_shapes (name, slug, icon_url) VALUES (%s, %s, %s) RETURNING id;",
            (shape["name"], shape["slug"], shape["icon_url"])
        )
        shape_id = cur.fetchone()[0]
        shape_id_map[shape["name"].lower()] = shape_id
        print(f"  [Shape ID {shape_id}] {shape['name']} ({shape['slug']})")
    return shape_id_map


def crawl_and_seed_products(conn, cur, category_id_map, shape_id_map):
    """Crawls /Users/sahil/Desktop/Image and ingests products + product_images."""
    if not os.path.exists(DESKTOP_IMAGE_PATH):
        print(f"ERROR: Image repository path '{DESKTOP_IMAGE_PATH}' not found!")
        sys.exit(1)

    print(f"Scanning Desktop Image repository at '{DESKTOP_IMAGE_PATH}'...")
    
    total_products_inserted = 0
    total_images_inserted = 0

    category_folders = [d for d in os.listdir(DESKTOP_IMAGE_PATH) if os.path.isdir(os.path.join(DESKTOP_IMAGE_PATH, d))]
    
    for cat_folder in sorted(category_folders):
        cat_key = cat_folder.lower()
        if cat_key not in category_id_map:
            print(f"Skipping unknown category folder: {cat_folder}")
            continue

        cat_id = category_id_map[cat_key]
        cat_path = os.path.join(DESKTOP_IMAGE_PATH, cat_folder)

        shape_folders = [s for s in os.listdir(cat_path) if os.path.isdir(os.path.join(cat_path, s))]
        
        for shape_folder in sorted(shape_folders):
            shape_key = shape_folder.lower()
            if shape_key not in shape_id_map:
                print(f"Skipping unknown shape folder: {shape_folder}")
                continue

            shape_id = shape_id_map[shape_key]
            shape_path = os.path.join(cat_path, shape_folder)

            product_folders = [p for p in os.listdir(shape_path) if os.path.isdir(os.path.join(shape_path, p))]

            print(f"Ingesting '{cat_folder}' -> '{shape_folder}' ({len(product_folders)} products)...")

            for prod_folder in sorted(product_folders):
                title = prod_folder.strip()
                prod_slug = slugify(title)
                price = calculate_price(title, cat_folder)
                prod_dir_path = os.path.join(shape_path, prod_folder)

                # Get image files inside product folder
                image_files = sorted([
                    f for f in os.listdir(prod_dir_path) 
                    if f.lower().endswith(('.png', '.jpg', '.jpeg', '.webp'))
                ])

                if not image_files:
                    continue

                # Insert Product
                cur.execute(
                    "INSERT INTO products (title, slug, price, category_id, diamond_shape_id) VALUES (%s, %s, %s, %s, %s) RETURNING id;",
                    (title, prod_slug, price, cat_id, shape_id)
                )
                product_id = cur.fetchone()[0]
                total_products_inserted += 1

                # Target directory in wwwroot/assets/products for fast static web serving
                cat_slug = CATEGORIES[[c['name'].lower() for c in CATEGORIES].index(cat_key)]['slug']
                shape_slug = DIAMOND_SHAPES[[s['name'].lower() for s in DIAMOND_SHAPES].index(shape_key)]['slug']
                dest_dir = os.path.join(WWWROOT_PRODUCTS_PATH, cat_slug, shape_slug, prod_slug)
                os.makedirs(dest_dir, exist_ok=True)

                # Insert Product Images
                image_records = []
                for order, img_file in enumerate(image_files, start=1):
                    src_file = os.path.join(prod_dir_path, img_file)
                    dest_file = os.path.join(dest_dir, img_file)
                    
                    # Fast copy if not existing
                    if not os.path.exists(dest_file):
                        shutil.copy2(src_file, dest_file)

                    web_image_path = f"/assets/products/{cat_slug}/{shape_slug}/{prod_slug}/{img_file}"
                    image_records.append((product_id, web_image_path, order))

                if image_records:
                    execute_values(
                        cur,
                        "INSERT INTO product_images (product_id, image_path, display_order) VALUES %s;",
                        image_records
                    )
                    total_images_inserted += len(image_records)

        conn.commit()

    print(f"\n✅ SEEDING COMPLETE!")
    print(f"  - Total Products Ingested: {total_products_inserted}")
    print(f"  - Total Product Images Ingested: {total_images_inserted}")


def main():
    conn = get_db_connection()
    cur = conn.cursor()

    try:
        reset_schema_and_tables(cur)
        conn.commit()

        category_id_map = seed_categories(cur)
        shape_id_map = seed_diamond_shapes(cur)
        conn.commit()

        crawl_and_seed_products(conn, cur, category_id_map, shape_id_map)

    except Exception as e:
        conn.rollback()
        print(f"\n❌ Error during database seeding: {e}")
        raise e
    finally:
        cur.close()
        conn.close()


if __name__ == "__main__":
    main()
