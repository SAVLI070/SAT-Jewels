#!/usr/bin/env python3
"""
=============================================================================
SAT JEWEL - METAL OPTIONS & PRODUCT VARIANTS SEEDER
=============================================================================
This script:
1. Clears existing data in `metals` and `product_variants` tables.
2. Seeds the 10 official Metal options from Aurous Fine Jewelry with color hex swatches.
3. Iterates over all existing products in the database and creates 10 product metal 
   variants per product (2,868 products × 10 metals = 28,680 variants!).
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


def get_db_connection():
    conn_str = os.environ.get("DATABASE_URL", DEFAULT_CONN_STRING)
    if conn_str.startswith("postgres://") or conn_str.startswith("postgresql://"):
        import urllib.parse
        url = urllib.parse.urlparse(conn_str)
        conn_str = f"host={url.hostname} port={url.port or 5432} dbname={url.path.lstrip('/')} user={url.username} password={url.password} sslmode=require"
    print("Connecting to database...")
    return psycopg2.connect(conn_str)


def reset_metals_and_variants_tables(cur):
    print("🚨 Clearing old data in metals and product_variants tables...")
    cur.execute("""
        DROP TABLE IF EXISTS product_variants CASCADE;
        DROP TABLE IF EXISTS "ProductVariants" CASCADE;
        DROP TABLE IF EXISTS metals CASCADE;
        DROP TABLE IF EXISTS "Metals" CASCADE;

        CREATE TABLE metals (
            id BIGSERIAL PRIMARY KEY,
            name VARCHAR(100) NOT NULL UNIQUE,
            slug VARCHAR(150) NOT NULL UNIQUE,
            color_group VARCHAR(50) NOT NULL,
            color_hex VARCHAR(20) NOT NULL
        );

        CREATE TABLE product_variants (
            id BIGSERIAL PRIMARY KEY,
            product_id BIGINT NOT NULL REFERENCES products(id) ON DELETE CASCADE,
            metal_id BIGINT NOT NULL REFERENCES metals(id) ON DELETE CASCADE,
            sku VARCHAR(100) NOT NULL,
            price NUMERIC(18, 2) NOT NULL DEFAULT 0.00,
            stock_quantity INT NOT NULL DEFAULT 10,
            variant_image_path VARCHAR(500) DEFAULT '',
            is_available BOOLEAN NOT NULL DEFAULT TRUE
        );

        CREATE INDEX idx_product_variants_product ON product_variants(product_id);
        CREATE INDEX idx_product_variants_metal ON product_variants(metal_id);
        CREATE INDEX idx_product_variants_prod_metal ON product_variants(product_id, metal_id);
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
        print(f"  [Metal ID {metal_id}] {m['name']} ({m['color_group']} - {m['color_hex']})")
    return metal_map


def seed_product_variants(conn, cur, metal_map):
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
    print(f"Found {len(products)} products to generate metal variants for...")

    variant_records = []
    for prod in products:
        p_id, p_slug, p_price, main_img = prod
        base_price = float(p_price)

        for metal in metal_map:
            m_id = metal["id"]
            m_slug = metal["slug"]
            m_mult = metal["multiplier"]

            v_sku = f"SAT-{p_id}-{m_slug.upper()}"
            v_price = round(base_price * m_mult, 2)
            v_stock = 15
            
            variant_records.append((p_id, m_id, v_sku, v_price, v_stock, main_img, True))

    print(f"Bulk inserting {len(variant_records)} product metal variants...")
    execute_values(
        cur,
        """
        INSERT INTO product_variants 
        (product_id, metal_id, sku, price, stock_quantity, variant_image_path, is_available) 
        VALUES %s;
        """,
        variant_records,
        page_size=5000
    )
    conn.commit()
    print(f"✅ Successfully seeded {len(variant_records)} Product Metal Variants!")


def main():
    conn = get_db_connection()
    cur = conn.cursor()

    try:
        reset_metals_and_variants_tables(cur)
        conn.commit()

        metal_map = seed_metals(cur)
        conn.commit()

        seed_product_variants(conn, cur, metal_map)

    except Exception as e:
        conn.rollback()
        print(f"\n❌ Error during metal seeding: {e}")
        raise e
    finally:
        cur.close()
        conn.close()


if __name__ == "__main__":
    main()
