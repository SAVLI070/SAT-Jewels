#!/usr/bin/env python3
"""
=============================================================================
SAT JEWEL - METALS, CARATS & PRODUCT VARIANTS SEEDER
=============================================================================
This script:
1. Clears old data in `metals`, `carat_options`, and `product_variants` tables.
2. Seeds the 10 Official Metal options and 9 Carat Weight options.
3. Iterates over all existing products in the database and generates 
   product variants combining metal choices and carat weight choices with 
   dynamic pricing calculations.
=============================================================================
"""

import os
import sys
import psycopg2
from psycopg2.extras import execute_values

DEFAULT_CONN_STRING = (
    "host=ep-soft-sound-azkeypgg-pooler.c-3.ap-southeast-1.aws.neon.tech "
    "port=5432 dbname=neondb user=neondb_owner password=npg_yX8TV4rmHEqR sslmode=require"
)

# 10 Official Metal Options from Aurous Fine Jewelry
METALS = [
    {"name": "10K Yellow Gold", "slug": "10k-yellow-gold", "color_group": "Yellow Gold", "color_hex": "#E5CA8F", "multiplier": 0.90},
    {"name": "10K White Gold",  "slug": "10k-white-gold",  "color_group": "White Gold",  "color_hex": "#D1D5DB", "multiplier": 0.90},
    {"name": "10K Rose Gold",   "slug": "10k-rose-gold",   "color_group": "Rose Gold",   "color_hex": "#E8A598", "multiplier": 0.90},
    {"name": "14K Yellow Gold", "slug": "14k-yellow-gold", "color_group": "Yellow Gold", "color_hex": "#F2D06B", "multiplier": 1.00},
    {"name": "14K White Gold",  "slug": "14k-white-gold",  "color_group": "White Gold",  "color_hex": "#E5E7EB", "multiplier": 1.00},
    {"name": "14K Rose Gold",   "slug": "14k-rose-gold",   "color_group": "Rose Gold",   "color_hex": "#EAA396", "multiplier": 1.00},
    {"name": "18K Yellow Gold", "slug": "18k-yellow-gold", "color_group": "Yellow Gold", "color_hex": "#FFD700", "multiplier": 1.15},
    {"name": "18K White Gold",  "slug": "18k-white-gold",  "color_group": "White Gold",  "color_hex": "#F5F5F5", "multiplier": 1.15},
    {"name": "18K Rose Gold",   "slug": "18k-rose-gold",   "color_group": "Rose Gold",   "color_hex": "#E68A7C", "multiplier": 1.15},
    {"name": "950 Platinum",    "slug": "950-platinum",    "color_group": "Platinum",    "color_hex": "#E5E4E2", "multiplier": 1.35}
]

# 9 Carat Weight Options
CARATS = [
    {"weight": 0.50, "label": "0.50 CT", "slug": "0.50-ct", "multiplier": 0.70},
    {"weight": 0.75, "label": "0.75 CT", "slug": "0.75-ct", "multiplier": 0.85},
    {"weight": 1.00, "label": "1.00 CT", "slug": "1.00-ct", "multiplier": 0.95},
    {"weight": 1.25, "label": "1.25 CT", "slug": "1.25-ct", "multiplier": 1.00},
    {"weight": 1.50, "label": "1.50 CT", "slug": "1.50-ct", "multiplier": 1.10},
    {"weight": 2.00, "label": "2.00 CT", "slug": "2.00-ct", "multiplier": 1.30},
    {"weight": 3.00, "label": "3.00 CT", "slug": "3.00-ct", "multiplier": 1.65},
    {"weight": 4.00, "label": "4.00 CT", "slug": "4.00-ct", "multiplier": 2.10},
    {"weight": 5.00, "label": "5.00 CT", "slug": "5.00-ct", "multiplier": 2.65}
]


def get_db_connection():
    conn_str = os.environ.get("DATABASE_URL", DEFAULT_CONN_STRING)
    if conn_str.startswith("postgres://") or conn_str.startswith("postgresql://"):
        import urllib.parse
        url = urllib.parse.urlparse(conn_str)
        conn_str = f"host={url.hostname} port={url.port or 5432} dbname={url.path.lstrip('/')} user={url.username} password={url.password} sslmode=require"
    print("Connecting to database...")
    return psycopg2.connect(conn_str)


def reset_schema_tables(cur):
    print("🚨 Clearing old data in metals, carat_options, and product_variants tables...")
    cur.execute("""
        DROP TABLE IF EXISTS product_variants CASCADE;
        DROP TABLE IF EXISTS "ProductVariants" CASCADE;
        DROP TABLE IF EXISTS metals CASCADE;
        DROP TABLE IF EXISTS "Metals" CASCADE;
        DROP TABLE IF EXISTS carat_options CASCADE;
        DROP TABLE IF EXISTS "CaratOptions" CASCADE;

        CREATE TABLE metals (
            id BIGSERIAL PRIMARY KEY,
            name VARCHAR(100) NOT NULL UNIQUE,
            slug VARCHAR(150) NOT NULL UNIQUE,
            color_group VARCHAR(50) NOT NULL,
            color_hex VARCHAR(20) NOT NULL
        );

        CREATE TABLE carat_options (
            id BIGSERIAL PRIMARY KEY,
            carat_weight NUMERIC(6, 2) NOT NULL UNIQUE,
            label VARCHAR(50) NOT NULL UNIQUE,
            slug VARCHAR(50) NOT NULL UNIQUE
        );

        CREATE TABLE product_variants (
            id BIGSERIAL PRIMARY KEY,
            product_id BIGINT NOT NULL REFERENCES products(id) ON DELETE CASCADE,
            metal_id BIGINT NOT NULL REFERENCES metals(id) ON DELETE CASCADE,
            carat_id BIGINT REFERENCES carat_options(id) ON DELETE SET NULL,
            sku VARCHAR(100) NOT NULL,
            price NUMERIC(18, 2) NOT NULL DEFAULT 0.00,
            stock_quantity INT NOT NULL DEFAULT 10,
            variant_image_path VARCHAR(500) DEFAULT '',
            is_available BOOLEAN NOT NULL DEFAULT TRUE
        );

        CREATE INDEX idx_product_variants_product ON product_variants(product_id);
        CREATE INDEX idx_product_variants_metal ON product_variants(metal_id);
        CREATE INDEX idx_product_variants_carat ON product_variants(carat_id);
        CREATE INDEX idx_product_variants_multi ON product_variants(product_id, metal_id, carat_id);
    """)
    print("Tables reset clean!")


def seed_metals(cur):
    print("Seeding 10 Official Metal Options...")
    metal_map = []
    for m in METALS:
        cur.execute(
            "INSERT INTO metals (name, slug, color_group, color_hex) VALUES (%s, %s, %s, %s) RETURNING id;",
            (m["name"], m["slug"], m["color_group"], m["color_hex"])
        )
        metal_id = cur.fetchone()[0]
        m_copy = dict(m)
        m_copy["id"] = metal_id
        metal_map.append(m_copy)
        print(f"  [Metal ID {metal_id}] {m['name']} ({m['color_hex']})")
    return metal_map


def seed_carat_options(cur):
    print("Seeding 9 Carat Weight Options...")
    carat_map = []
    for c in CARATS:
        cur.execute(
            "INSERT INTO carat_options (carat_weight, label, slug) VALUES (%s, %s, %s) RETURNING id;",
            (c["weight"], c["label"], c["slug"])
        )
        carat_id = cur.fetchone()[0]
        c_copy = dict(c)
        c_copy["id"] = carat_id
        carat_map.append(c_copy)
        print(f"  [Carat ID {carat_id}] {c['label']} ({c['weight']} CT)")
    return carat_map


def seed_product_variants(conn, cur, metal_map, carat_map):
    print("Fetching existing products and main images...")
    cur.execute("""
        SELECT p.id, p.slug, p.price, COALESCE(pi.image_path, '/assets/hero_1.jpg') as main_image
        FROM products p
        LEFT JOIN (
            SELECT DISTINCT ON (product_id) product_id, image_path 
            FROM product_images 
            ORDER BY product_id, display_order
        ) pi ON p.id = pi.product_id;
    """)
    products = cur.fetchall()
    print(f"Found {len(products)} products...")

    variant_records = []
    # To keep DB optimal, we generate metal variants for each metal (10 metals per product),
    # with popular carat options assigned (e.g. 1.50 CT default + sample carats per metal).
    for prod in products:
        p_id, p_slug, p_price, main_img = prod
        base_price = float(p_price)

        for metal in metal_map:
            m_id = metal["id"]
            m_slug = metal["slug"]
            m_mult = metal["multiplier"]

            for carat in carat_map:
                c_id = carat["id"]
                c_slug = carat["slug"]
                c_mult = carat["multiplier"]

                # Generate unique variant SKU
                v_sku = f"SAT-{p_id}-{m_slug.upper()}-{c_slug.upper()}"
                
                # Dynamic Price Calculation: Base Price × Metal Multiplier × Carat Multiplier
                v_price = round(base_price * m_mult * c_mult, 2)
                v_stock = 15
                
                variant_records.append((p_id, m_id, c_id, v_sku, v_price, v_stock, main_img, True))

    print(f"Bulk inserting {len(variant_records)} product variants...")
    execute_values(
        cur,
        """
        INSERT INTO product_variants 
        (product_id, metal_id, carat_id, sku, price, stock_quantity, variant_image_path, is_available) 
        VALUES %s;
        """,
        variant_records,
        page_size=10000
    )
    conn.commit()
    print(f"✅ Successfully seeded {len(variant_records)} Product Variants!")


def main():
    conn = get_db_connection()
    cur = conn.cursor()

    try:
        reset_schema_tables(cur)
        conn.commit()

        metal_map = seed_metals(cur)
        carat_map = seed_carat_options(cur)
        conn.commit()

        seed_product_variants(conn, cur, metal_map, carat_map)

    except Exception as e:
        conn.rollback()
        print(f"\n❌ Error during seeding: {e}")
        raise e
    finally:
        cur.close()
        conn.close()


if __name__ == "__main__":
    main()
