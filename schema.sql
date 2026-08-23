-- ============================================================
-- SAT JEWEL - DATABASE SCHEMA CREATION & CLEANUP SCRIPT
-- ============================================================

-- 1. DROP EXISTING TABLES IN CASCADE ORDER (TRUNCATE / RESET ALL DATA)
DROP TABLE IF EXISTS product_variants CASCADE;
DROP TABLE IF EXISTS "ProductVariants" CASCADE;
DROP TABLE IF EXISTS product_images CASCADE;
DROP TABLE IF EXISTS "ProductImages" CASCADE;
DROP TABLE IF EXISTS products CASCADE;
DROP TABLE IF EXISTS "Products" CASCADE;
DROP TABLE IF EXISTS metals CASCADE;
DROP TABLE IF EXISTS "Metals" CASCADE;
DROP TABLE IF EXISTS carat_options CASCADE;
DROP TABLE IF EXISTS "CaratOptions" CASCADE;
DROP TABLE IF EXISTS diamond_shapes CASCADE;
DROP TABLE IF EXISTS "DiamondShapes" CASCADE;
DROP TABLE IF EXISTS categories CASCADE;
DROP TABLE IF EXISTS "Categories" CASCADE;

-- 2. CREATE CATEGORIES TABLE
CREATE TABLE categories (
    id BIGSERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE,
    slug VARCHAR(150) NOT NULL UNIQUE
);

-- 3. CREATE DIAMOND SHAPES TABLE
CREATE TABLE diamond_shapes (
    id BIGSERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE,
    slug VARCHAR(150) NOT NULL UNIQUE,
    icon_url VARCHAR(500) DEFAULT ''
);

-- 4. CREATE METALS TABLE (10 OFFICIAL METALS FROM AUROUS FINE JEWELRY)
CREATE TABLE metals (
    id BIGSERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL UNIQUE,
    slug VARCHAR(150) NOT NULL UNIQUE,
    color_group VARCHAR(50) NOT NULL,
    color_hex VARCHAR(20) NOT NULL
);

-- 5. CREATE CARAT OPTIONS TABLE
CREATE TABLE carat_options (
    id BIGSERIAL PRIMARY KEY,
    carat_weight NUMERIC(6, 2) NOT NULL UNIQUE,
    label VARCHAR(50) NOT NULL UNIQUE,
    slug VARCHAR(50) NOT NULL UNIQUE
);

-- 6. CREATE PRODUCTS TABLE
CREATE TABLE products (
    id BIGSERIAL PRIMARY KEY,
    title VARCHAR(255) NOT NULL,
    slug VARCHAR(255) NOT NULL,
    price NUMERIC(18, 2) NOT NULL DEFAULT 0.00,
    category_id BIGINT NOT NULL REFERENCES categories(id) ON DELETE CASCADE,
    diamond_shape_id BIGINT NOT NULL REFERENCES diamond_shapes(id) ON DELETE CASCADE,
    created_at TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP
);

-- CREATE PERFORMANCE INDEXES FOR FAST FILTERING
CREATE INDEX idx_products_category ON products(category_id);
CREATE INDEX idx_products_diamond_shape ON products(diamond_shape_id);
CREATE INDEX idx_products_cat_shape ON products(category_id, diamond_shape_id);
CREATE INDEX idx_products_created_at ON products(created_at DESC);

-- 7. CREATE PRODUCT IMAGES TABLE
CREATE TABLE product_images (
    id BIGSERIAL PRIMARY KEY,
    product_id BIGINT NOT NULL REFERENCES products(id) ON DELETE CASCADE,
    image_path VARCHAR(500) NOT NULL,
    display_order INT NOT NULL DEFAULT 1
);

CREATE INDEX idx_product_images_product ON product_images(product_id);
CREATE INDEX idx_product_images_order ON product_images(product_id, display_order);

-- 8. CREATE PRODUCT VARIANTS TABLE
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
