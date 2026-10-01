import os
import io
import re
import sys
import time
import json
import zipfile
import psycopg2
from PIL import Image
import cloudinary
import cloudinary.uploader
from concurrent.futures import ThreadPoolExecutor, as_completed

# Cloudinary Configuration
cloudinary.config(
    cloud_name='ihcs8m6o',
    api_key='826999397858529',
    api_secret='S8guO0Os21rzu4vxKAztT39irto'
)

# RDS PostgreSQL Connection
DB_URL = "postgresql://satjewels_admin:SatJewels%23Db2026%21Secure@satjewels-postgres.c4r4s48oeqi1.us-east-1.rds.amazonaws.com:5432/satjewels_db"
STATE_FILE = "/Users/sahil/Desktop/SAT1/ingest_state.json"

# Detailed classifications for non-round rings and earrings
custom_mapping = {
    # === alibaba new 2 photos (13 items) ===
    'RG 1374-14K-7.zip': {'shape': 1, 'cat': 2, 'carat': 1.0, 'title': 'Alternating Round Bezel Eternity Wedding Band RG1374'},
    'RG1013-14K-7-1ct.zip': {'shape': 2, 'cat': 1, 'carat': 1.0, 'title': 'Classic Oval Cut Solitaire Diamond Engagement Ring RG1013'},
    'RG1211-14K-7-1ct.zip': {'shape': 4, 'cat': 1, 'carat': 1.0, 'title': 'Elongated Marquise Cut Solitaire Diamond Ring RG1211'},
    'RG1260-14K-1ct-7.zip': {'shape': 3, 'cat': 1, 'carat': 1.0, 'title': 'Modern Emerald Cut Bezel Solitaire Diamond Ring RG1260'},
    'RG1281-14K-1ct-7.zip': {'shape': 1, 'cat': 1, 'carat': 1.0, 'title': 'Asymmetric Cluster Round Diamond Engagement Ring RG1281'},
    'RG1305-14K-1ct-7.zip': {'shape': 2, 'cat': 1, 'carat': 1.0, 'title': 'Twisted Vine Oval Cut Diamond Solitaire Ring RG1305'},
    'RG1306-14K-1ct-7.zip': {'shape': 3, 'cat': 1, 'carat': 1.0, 'title': 'Three-Stone Emerald Cut Diamond Engagement Ring RG1306'},
    'RG1316-14K-1ct-7.zip': {'shape': 2, 'cat': 1, 'carat': 1.0, 'title': 'Botanical Floral Oval Cut Diamond Ring RG1316'},
    'RG1338-14K-0.5ct-7.zip': {'shape': 4, 'cat': 1, 'carat': 0.5, 'title': 'Floating Marquise Open Bypass Diamond Ring RG1338'},
    'RG1340-14K-7.zip': {'shape': 2, 'cat': 2, 'carat': 1.0, 'title': 'Oval Cut Bezel Setting Eternity Wedding Band RG1340'},
    'RG1356-14K-1ct-7.zip': {'shape': 2, 'cat': 1, 'carat': 1.0, 'title': 'Braided Rope Shank Oval Cut Diamond Engagement Ring RG1356'},
    'RG1368-14K-0.5ct-7.zip': {'shape': 4, 'cat': 1, 'carat': 0.5, 'title': 'Petite Marquise Cut Lab Diamond Solitaire Ring RG1368'},
    'RG1386-14K-1ct-7.zip': {'shape': 4, 'cat': 1, 'carat': 1.0, 'title': 'Nature Leaf Marquise Cut Lab Diamond Engagement Ring RG1386'},

    # === alibaba new photos (43 items) ===
    'RG1008-14K-7-1ct.zip': {'shape': 7, 'cat': 1, 'carat': 1.0, 'title': 'Cushion Cut Four-Prong Solitaire Diamond Ring RG1008'},
    'RG1045-14K-7-1ct.zip': {'shape': 1, 'cat': 1, 'carat': 1.0, 'title': 'Round Brilliant Diamond Pavé Accent Engagement Ring RG1045'},
    'RG1104-14K-7.zip': {'shape': 1, 'cat': 3, 'carat': 1.0, 'title': "Men's Channel Set Diamond Eternity Band RG1104"},
    'RG1107-14K-7.zip': {'shape': 6, 'cat': 3, 'carat': 1.0, 'title': "Men's Princess Cut Flush Signet Band RG1107"},
    'RG1110-14K-7.zip': {'shape': 6, 'cat': 3, 'carat': 1.0, 'title': "Men's Flush Set Princess Cut Wedding Band RG1110"},
    'RG1112-14K-7.zip': {'shape': 1, 'cat': 3, 'carat': 1.0, 'title': "Men's Bezel Set Round Solitaire Signet Ring RG1112"},
    'RG1115-14K-7.zip': {'shape': 1, 'cat': 3, 'carat': 1.5, 'title': "Men's Royal Pave Round Diamond Signet Ring RG1115"},
    'RG1131-14K-7-1ct.zip': {'shape': 1, 'cat': 1, 'carat': 1.0, 'title': 'Round Brilliant Three-Stone Diamond Engagement Ring RG1131'},
    'RG1138-14K-7.zip': {'shape': 6, 'cat': 3, 'carat': 1.0, 'title': "Men's Modern Princess Cut Solitaire Ring RG1138"},
    'RG1142-14K-7.zip': {'shape': 6, 'cat': 3, 'carat': 1.0, 'title': "Men's Brushed Gold Princess Diamond Band RG1142"},
    'RG1143-14K-7.zip': {'shape': 6, 'cat': 3, 'carat': 1.0, 'title': "Men's Frosted White Gold Princess Diamond Band RG1143"},
    'RG1145-14K-7-2ct.zip': {'shape': 1, 'cat': 1, 'carat': 2.0, 'title': 'Round Brilliant Cathedral Solitaire Diamond Ring RG1145'},
    'RG1146-14K-7-2ct.zip': {'shape': 1, 'cat': 1, 'carat': 2.0, 'title': 'Round Cut Hidden Halo Diamond Engagement Ring RG1146'},
    'RG1148-14K-7-2ct.zip': {'shape': 1, 'cat': 1, 'carat': 2.0, 'title': 'Round Brilliant Four-Prong Wire Basket Ring RG1148'},
    'RG1150-14K-7-2ct.zip': {'shape': 7, 'cat': 1, 'carat': 2.0, 'title': 'Cushion Cut Hidden Halo Solitaire Engagement Ring RG1150'},
    'RG1153-14K-7-2ct.zip': {'shape': 6, 'cat': 1, 'carat': 2.0, 'title': 'Princess Cut Knife-Edge Solitaire Diamond Ring RG1153'},
    'RG1157-14K-7-2ct.zip': {'shape': 7, 'cat': 1, 'carat': 2.0, 'title': 'Cushion Cut Wire Basket Solitaire Ring RG1157'},
    'RG1158-14K-7-2ct.zip': {'shape': 1, 'cat': 1, 'carat': 2.0, 'title': 'Round Brilliant Slender Shank Solitaire Ring RG1158'},
    'RG1161-14K-2ct-7.zip': {'shape': 1, 'cat': 1, 'carat': 2.0, 'title': 'Round Cut Pavé Cathedral Diamond Engagement Ring RG1161'},
    'RG1166-14K-7-1ct.zip': {'shape': 1, 'cat': 1, 'carat': 1.0, 'title': 'Round Brilliant Half-Bezel Modern Solitaire Ring RG1166'},
    'RG1167-14K-7-1ct.zip': {'shape': 8, 'cat': 1, 'carat': 1.0, 'title': 'Radiant Cut Pavé Hidden Wrap Engagement Ring RG1167'},
    'RG1168-14K-7-1ct.zip': {'shape': 7, 'cat': 1, 'carat': 1.0, 'title': 'Cushion Cut Petite Solitaire Diamond Engagement Ring RG1168'},
    'RG1171-14K-7-1ct.zip': {'shape': 8, 'cat': 1, 'carat': 1.0, 'title': 'Fancy Pink Radiant Cut Three-Stone Diamond Ring RG1171'},
    'RG1191-14K-7.zip': {'shape': 1, 'cat': 1, 'carat': 1.0, 'title': 'Modern Tension Bypass Round Diamond Engagement Ring RG1191'},
    'RG1198-14K-1ct-7.zip': {'shape': 7, 'cat': 1, 'carat': 1.0, 'title': 'Cushion Cut Four-Prong Wire Solitaire Ring RG1198'},
    'RG1202-14K-1ct-7.zip': {'shape': 1, 'cat': 1, 'carat': 1.0, 'title': 'Lotus Petal Round Brilliant Solitaire Ring RG1202'},
    'RG1203-14K-7-2ct.zip': {'shape': 1, 'cat': 1, 'carat': 2.0, 'title': 'Lotus Prong Round Pavé Diamond Engagement Ring RG1203'},
    'RG1216-14K-1ct-7.zip': {'shape': 1, 'cat': 1, 'carat': 1.0, 'title': 'Classic Brilliant Round Four-Prong Solitaire Ring RG1216'},
    'RG1223-14K-7-1ct.zip': {'shape': 1, 'cat': 1, 'carat': 1.0, 'title': 'Round Brilliant Slender Solitaire Ring RG1223'},
    'RG1226-14K-1ct-7.zip': {'shape': 1, 'cat': 1, 'carat': 1.0, 'title': 'Floral Bloom Round Brilliant Diamond Ring RG1226'},
    'RG1252-14K-1ct-7.zip': {'shape': 1, 'cat': 1, 'carat': 1.0, 'title': 'Pavé Bridge Round Brilliant Cathedral Ring RG1252'},
    'RG1278-14K-1ct-7.zip': {'shape': 1, 'cat': 1, 'carat': 1.0, 'title': 'Round Brilliant Solitaire with Diamond Eternity Band Set RG1278'},
    'RG1308-14K-7.zip': {'shape': 1, 'cat': 3, 'carat': 0.75, 'title': "Men's Flush Set Tension Round Diamond Band RG1308"},
    'RG1309-14K-7.zip': {'shape': 1, 'cat': 3, 'carat': 0.75, 'title': "Men's Bypass Channel Round Diamond Band RG1309"},
    'RG1312-14K-7.zip': {'shape': 6, 'cat': 3, 'carat': 1.0, 'title': "Men's Pavé Border Princess Diamond Signet Ring RG1312"},
    'RG1319-14K-7.zip': {'shape': 1, 'cat': 2, 'carat': 0.5, 'title': 'Geometric Faceted Round Diamond Wedding Band RG1319'},
    'RG1326-14K-1ct-7.zip': {'shape': 1, 'cat': 1, 'carat': 1.0, 'title': 'Lotus Basket Round Brilliant Diamond Solitaire Ring RG1326'},
    'RG1335-14K-7.zip': {'shape': 1, 'cat': 2, 'carat': 0.5, 'title': 'Curved Contour Diamond Chevron Wedding Band RG1335'},
    'RG1349-14K-0.5ct-7.zip': {'shape': 1, 'cat': 3, 'carat': 0.5, 'title': "Men's Modern Bezel Flush Diamond Ring RG1349"},
    'RG1353-14K-1ct-7.zip': {'shape': 1, 'cat': 1, 'carat': 1.0, 'title': 'Twisted Rope Shank Round Brilliant Solitaire Ring RG1353'},
    'RG1358-14K-1ct-7.zip': {'shape': 6, 'cat': 1, 'carat': 1.0, 'title': 'Twisted Rope Princess Cut Solitaire Engagement Ring RG1358'},
    'RG1367-14K-0.5ct-7.zip': {'shape': 7, 'cat': 1, 'carat': 0.5, 'title': 'Delicate Petite Cushion Cut Solitaire Ring RG1367'},
    'RG1388-14K-1ct-7.zip': {'shape': 6, 'cat': 1, 'carat': 1.0, 'title': 'Botanical Vine Princess Cut Diamond Engagement Ring RG1388'},

    # === ALI EARRING PHOTO 1 (47 items) ===
    'ST1004-14K-1ct.zip': {'shape': 5, 'cat': 4, 'carat': 1.0, 'title': 'Signature Teardrop Pear Cut Diamond Stud Earrings ST1004'},
    'ST1010-14K-1ct.zip': {'shape': 1, 'cat': 4, 'carat': 1.0, 'title': 'Classic Brilliant Round Diamond 4-Prong Stud Earrings ST1010'},
    'ST1020-14K.zip': {'shape': 1, 'cat': 4, 'carat': 0.5, 'title': 'Pave Set Curved Diamond Huggie Hoop Earrings ST1020'},
    'ST1038-14K.zip': {'shape': 1, 'cat': 4, 'carat': 0.5, 'title': 'Minimalist Bezel Round Diamond Huggie Earrings ST1038'},
    'ST1041-14K.zip': {'shape': 6, 'cat': 4, 'carat': 1.0, 'title': 'Princess Cut Diamond Micro-Pavé Halo Stud Earrings ST1041'},
    'ST1047-18K.zip': {'shape': 5, 'cat': 4, 'carat': 1.0, 'title': 'Ruby & Pear Diamond Drop Dangle Earrings ST1047'},
    'ST1048-14K.zip': {'shape': 5, 'cat': 4, 'carat': 1.0, 'title': 'Two-Stone Round & Pear Cut Diamond Ear Jacket Earrings ST1048'},
    'ST1049-14K.zip': {'shape': 3, 'cat': 4, 'carat': 1.5, 'title': 'Triple Tier Emerald Cut Diamond Drop Earrings ST1049'},
    'ST1050-14K.zip': {'shape': 1, 'cat': 4, 'carat': 1.0, 'title': 'Starburst Floral Round Diamond Cluster Stud Earrings ST1050'},
    'ST1051-14K.zip': {'shape': 1, 'cat': 4, 'carat': 0.75, 'title': 'Geometric Pavé Triangular Diamond Drop Earrings ST1051'},
    'ST1052-14K.zip': {'shape': 2, 'cat': 4, 'carat': 1.0, 'title': 'Double Oval Cut Diamond Cluster Stud Earrings ST1052'},
    'ST1053-14K.zip': {'shape': 1, 'cat': 4, 'carat': 0.75, 'title': 'Halo Bezel Round Diamond Huggie Drop Earrings ST1053'},
    'ST1054-14K.zip': {'shape': 2, 'cat': 4, 'carat': 1.0, 'title': 'Oval Cut Solitaire Diamond Huggie Dangle Earrings ST1054'},
    'ST1055-14K.zip': {'shape': 3, 'cat': 4, 'carat': 1.0, 'title': 'Emerald Cut Solitaire Diamond Huggie Dangle Earrings ST1055'},
    'ST1056-14K.zip': {'shape': 8, 'cat': 4, 'carat': 1.0, 'title': 'Fancy Canary Radiant Cut Diamond Bezel Stud Earrings ST1056'},
    'ST1057-14K.zip': {'shape': 7, 'cat': 4, 'carat': 1.5, 'title': 'Two-Tone Cushion Cut Diamond Halo Stud Earrings ST1057'},
    'ST1058-14K.zip': {'shape': 3, 'cat': 4, 'carat': 1.0, 'title': 'Octagon Emerald Cut Diamond Bezel Drop Earrings ST1058'},
    'ST1060-14K-1ct.zip': {'shape': 10, 'cat': 4, 'carat': 1.0, 'title': 'Heart Shaped Solitaire Diamond Stud Earrings in Yellow Gold ST1060'},
    'ST1061-14K-1ct.zip': {'shape': 10, 'cat': 4, 'carat': 1.0, 'title': 'Heart Shaped Solitaire Diamond Stud Earrings in Rose Gold ST1061'},
    'ST1062-14K.zip': {'shape': 10, 'cat': 4, 'carat': 1.0, 'title': 'Heart Cut Diamond Huggie Dangle Drop Earrings ST1062'},
    'ST1063-14K.zip': {'shape': 5, 'cat': 4, 'carat': 1.0, 'title': 'Pear Cut Diamond Pavé Huggie Dangle Earrings ST1063'},
    'ST1064-14K.zip': {'shape': 7, 'cat': 4, 'carat': 1.5, 'title': 'Double Tier Cushion Cut Diamond Drop Dangle Earrings ST1064'},
    'ST1065-14K.zip': {'shape': 10, 'cat': 4, 'carat': 0.75, 'title': 'Geometric Heart Silhouette Diamond Stud Earrings ST1065'},
    'ST1066-14K.zip': {'shape': 3, 'cat': 4, 'carat': 1.0, 'title': 'Multi-Stone Emerald & Baguette Diamond Bypass Studs ST1066'},
    'ST1067-14K.zip': {'shape': 11, 'cat': 4, 'carat': 1.0, 'title': 'Multi-Cut Curved Diamond Arc Ear Climbers ST1067'},
    'ST1068-14K.zip': {'shape': 8, 'cat': 4, 'carat': 1.0, 'title': 'Radiant & Emerald Asymmetric Duo Diamond Studs ST1068'},
    'ST1069-14K.zip': {'shape': 1, 'cat': 4, 'carat': 0.5, 'title': 'Petite Pavé Round Diamond Huggie Hoop Earrings ST1069'},
    'ST1070-14K.zip': {'shape': 10, 'cat': 4, 'carat': 1.0, 'title': 'Toi et Moi Emerald & Heart Cut Diamond Duo Studs ST1070'},
    'ST1071-14K-0.5ct.zip': {'shape': 7, 'cat': 4, 'carat': 0.5, 'title': 'Cushion Cut Twisted Rope Halo Diamond Studs ST1071'},
    'ST1073-14K-0.5ct.zip': {'shape': 3, 'cat': 4, 'carat': 0.5, 'title': 'Emerald Cut Twisted Rope Halo Diamond Studs ST1073'},
    'ST1074-14K-0.5ct.zip': {'shape': 10, 'cat': 4, 'carat': 0.5, 'title': 'Heart Cut Twisted Rope Halo Diamond Studs ST1074'},
    'ST1075-14K-0.5ct.zip': {'shape': 1, 'cat': 4, 'carat': 0.5, 'title': 'Round Cut Twisted Rope Halo Diamond Huggie Studs ST1075'},
    'ST1076-14K-0.5ct.zip': {'shape': 2, 'cat': 4, 'carat': 0.5, 'title': 'Oval Cut Twisted Rope Halo Diamond Studs ST1076'},
    'ST1077-14K-0.5ct.zip': {'shape': 5, 'cat': 4, 'carat': 0.5, 'title': 'Pear Cut Twisted Rope Halo Diamond Studs ST1077'},
    'ST1078-14K-0.5ct.zip': {'shape': 6, 'cat': 4, 'carat': 0.5, 'title': 'Princess Cut Twisted Rope Halo Diamond Studs ST1078'},
    'ST1079-14K-0.5ct.zip': {'shape': 1, 'cat': 4, 'carat': 0.5, 'title': 'Round Brilliant Twisted Rope Halo Diamond Studs ST1079'},
    'ST1080-14K.zip': {'shape': 1, 'cat': 4, 'carat': 0.75, 'title': 'Celestial Starburst Round Diamond Stud Earrings ST1080'},
    'ST1083-14K.zip': {'shape': 1, 'cat': 4, 'carat': 0.75, 'title': 'Fluttering Butterfly Diamond Pavé Stud Earrings ST1083'},
    'ST1084-14K.zip': {'shape': 1, 'cat': 4, 'carat': 0.5, 'title': 'Infinity Knot Round Diamond Pavé Stud Earrings ST1084'},
    'ST1086-14K.zip': {'shape': 1, 'cat': 4, 'carat': 0.5, 'title': 'North Star Dainty Diamond Stud Earrings ST1086'},
    'ST1088-14K-0.5 ct.zip': {'shape': 3, 'cat': 4, 'carat': 0.5, 'title': 'Emerald Cut Modern Half-Bezel Diamond Studs ST1088'},
    'ST1089-14K-0.5 ct.zip': {'shape': 9, 'cat': 4, 'carat': 0.5, 'title': 'Asscher Cut Modern Half-Bezel Diamond Studs ST1089'},
    'ST1090-14K-0.5 ct.zip': {'shape': 2, 'cat': 4, 'carat': 0.5, 'title': 'Oval Cut Modern Half-Bezel Diamond Studs ST1090'},
    'ST1091-14K-0.5 ct.zip': {'shape': 6, 'cat': 4, 'carat': 0.5, 'title': 'Princess Cut Modern Half-Bezel Diamond Studs ST1091'},
    'ST1092-14K-0.5 ct.zip': {'shape': 1, 'cat': 4, 'carat': 0.5, 'title': 'Round Brilliant Modern Half-Bezel Diamond Studs ST1092'},
    'ST1093-14K-0.5 ct.zip': {'shape': 8, 'cat': 4, 'carat': 0.5, 'title': 'Radiant Cut Modern Basket Diamond Studs ST1093'},
    'ST1094-14K.zip': {'shape': 5, 'cat': 4, 'carat': 1.0, 'title': 'Teardrop Pear Cut Diamond Drop Dangle Earrings ST1094'}
}

# Pricing & carat configuration mapping
carat_options_map = {
    0.25: (850, [1, 2], ["0.50 CT"]),
    0.5:  (950, [1, 2, 3], ["0.50 CT", "0.75 CT (+150 USD)", "1.00 CT (+350 USD)"]),
    0.75: (1150, [1, 2, 3], ["0.50 CT (-150 USD)", "0.75 CT", "1.00 CT (+200 USD)"]),
    1.0:  (1350, [3, 4, 5, 6], ["1.00 CT", "1.25 CT (+200 USD)", "1.50 CT (+450 USD)", "2.00 CT (+950 USD)"]),
    1.25: (1550, [3, 4, 5], ["1.00 CT (-200 USD)", "1.25 CT", "1.50 CT (+250 USD)"]),
    1.5:  (1750, [4, 5, 6, 7], ["1.25 CT (-250 USD)", "1.50 CT", "2.00 CT (+500 USD)", "3.00 CT (+1400 USD)"]),
    2.0:  (2250, [5, 6, 7], ["1.50 CT (-500 USD)", "2.00 CT", "3.00 CT (+900 USD)"]),
    3.0:  (3150, [6, 7, 8], ["2.00 CT (-900 USD)", "3.00 CT", "4.00 CT (+1200 USD)"])
}

configured_metals = [
    (4, "14K Yellow Gold", 0),
    (5, "14K White Gold", 0),
    (6, "14K Rose Gold", 0),
    (7, "18K Yellow Gold", 300),
    (8, "18K White Gold", 300),
    (10, "950 Platinum", 650)
]
metal_options_str = "14K Yellow Gold|14K White Gold|14K Rose Gold|18K Yellow Gold (+300 USD)|18K White Gold (+300 USD)|950 Platinum (+650 USD)"

def upload_single_image(raw_bytes, sku_clean, idx):
    try:
        im = Image.open(io.BytesIO(raw_bytes)).convert('RGB')
        im.thumbnail((1920, 1920), Image.Resampling.LANCZOS)
        buf = io.BytesIO()
        im.save(buf, format='JPEG', quality=88, optimize=True)
        buf.seek(0)
        
        pub_id = f"{sku_clean}_{idx}_{int(time.time()*1000)%100000}"
        res = cloudinary.uploader.upload(
            buf,
            folder=f"sat_jewels/catalog/batch_oct2026/{sku_clean}",
            public_id=pub_id,
            overwrite=True
        )
        return (idx, res.get('secure_url'))
    except Exception as e:
        print(f"  [Cloudinary Error] {sku_clean} image {idx}: {e}")
        return (idx, None)

def get_product_images_raw(item_path):
    """Returns list of image bytes from either a .zip or a folder."""
    images = []
    if os.path.isdir(item_path):
        fnames = sorted([f for f in os.listdir(item_path) if f.lower().endswith(('.jpg', '.jpeg', '.png')) and not f.startswith('.')])
        for fn in fnames:
            with open(os.path.join(item_path, fn), 'rb') as f:
                images.append((fn, f.read()))
    elif item_path.endswith('.zip'):
        with zipfile.ZipFile(item_path, 'r') as z:
            fnames = sorted([f for f in z.namelist() if f.lower().endswith(('.jpg', '.jpeg', '.png')) and not f.startswith('__MACOSX') and not os.path.basename(f).startswith('.')])
            for fn in fnames:
                images.append((fn, z.read(fn)))
    return images

def build_manifest():
    folders = [
        ('round ring', '/Users/sahil/Downloads/round ring'),
        ('ALI EARRING PHOTO 1', '/Users/sahil/Downloads/ALI EARRING PHOTO 1'),
        ('alibaba new photos', '/Users/sahil/Downloads/alibaba new photos'),
        ('alibaba new 2 photos', '/Users/sahil/Downloads/alibaba new 2 photos')
    ]
    manifest = []
    for group_name, folder_path in folders:
        items = sorted([x for x in os.listdir(folder_path) if not x.startswith('.') and not x.startswith('__MACOSX')])
        for it in items:
            full_path = os.path.join(folder_path, it)
            clean_name = it.replace('.zip', '')
            
            # Check if custom mapped
            if it in custom_mapping:
                meta = custom_mapping[it]
                cat_id = meta['cat']
                shape_id = meta['shape']
                carat_val = meta['carat']
                title = meta['title']
            elif group_name == 'round ring':
                m_carat = re.search(r'(\d+(?:\.\d+)?)\s*ct', clean_name, re.I)
                carat_val = float(m_carat.group(1)) if m_carat else 1.0
                sku_raw = clean_name.split('-')[0].strip()
                cat_id = 1 # Engagement Rings
                shape_id = 1 # Round
                title = f"Classic Brilliant Round Diamond Solitaire Engagement Ring {sku_raw}"
            else:
                print(f"Warning: unmapped item {it} in {group_name}")
                continue
            
            sku_match = re.search(r'([A-Z]{2}\s*\d+)', clean_name, re.I)
            sku_code = sku_match.group(1).replace(' ', '') if sku_match else clean_name.split('-')[0]
            
            manifest.append({
                'filename': it,
                'path': full_path,
                'sku': sku_code,
                'cat_id': cat_id,
                'shape_id': shape_id,
                'carat': carat_val,
                'title': title
            })
    return manifest

def main():
    print("=====================================================")
    print("   SAT JEWELS - BATCH PRODUCT & IMAGE INGESTION     ")
    print("=====================================================")
    
    # Load checkpoint
    completed_skus = set()
    if os.path.exists(STATE_FILE):
        try:
            with open(STATE_FILE, 'r') as f:
                data = json.load(f)
                completed_skus = set(data.get('completed_skus', []))
                print(f"Loaded {len(completed_skus)} previously completed products from state file.")
        except Exception as e:
            print(f"Could not read state file: {e}")

    manifest = build_manifest()
    print(f"Total manifest items to process: {len(manifest)}")

    conn = psycopg2.connect(DB_URL)
    conn.autocommit = False
    cur = conn.cursor()

    # Reset sequences to prevent any ID collision
    cur.execute("""
        DO $$
        BEGIN
            BEGIN PERFORM setval(pg_get_serial_sequence('products', 'id'), COALESCE((SELECT MAX(id) FROM products), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
            BEGIN PERFORM setval(pg_get_serial_sequence('product_variants', 'id'), COALESCE((SELECT MAX(id) FROM product_variants), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
            BEGIN PERFORM setval(pg_get_serial_sequence('product_images', 'id'), COALESCE((SELECT MAX(id) FROM product_images), 0) + 1, false); EXCEPTION WHEN OTHERS THEN NULL; END;
        END $$;
    """)
    conn.commit()

    total_inserted = 0

    for idx, item in enumerate(manifest, 1):
        sku = item['sku']
        clean_file = item['filename'].replace('.zip', '')
        unique_key = f"{sku}_{clean_file}"

        if sku in completed_skus or unique_key in completed_skus:
            print(f"[{idx}/{len(manifest)}] Already ingested: {item['title']} ({sku}). Skipping.")
            continue

        # Double check if product already in DB
        cur.execute("SELECT id FROM products WHERE slug LIKE %s LIMIT 1;", (f"%{sku.lower()}%",))
        row = cur.fetchone()
        if row:
            print(f"[{idx}/{len(manifest)}] Already in DB (ID {row[0]}): {item['title']}. Skipping.")
            completed_skus.add(sku)
            continue

        raw_images = get_product_images_raw(item['path'])
        if not raw_images:
            print(f"[{idx}/{len(manifest)}] [ERROR] No images found for {item['filename']}! Skipping.")
            continue

        print(f"\n[{idx}/{len(manifest)}] Ingesting {item['title']} ({sku}) with {len(raw_images)} images...")
        
        # Concurrent Cloudinary upload
        sku_clean = re.sub(r'[^a-zA-Z0-9_\-]', '_', clean_file)
        uploaded = []
        with ThreadPoolExecutor(max_workers=6) as executor:
            future_to_idx = {
                executor.submit(upload_single_image, img_bytes, sku_clean, img_idx): img_idx
                for img_idx, (_, img_bytes) in enumerate(raw_images)
            }
            for fut in as_completed(future_to_idx):
                res = fut.result()
                if res[1]:
                    uploaded.append(res)
        
        uploaded.sort(key=lambda x: x[0])
        image_urls = [u for _, u in uploaded]
        if not image_urls:
            print(f"  Failed to upload images for {sku}, skipping DB insert.")
            continue

        main_img = image_urls[0]
        gallery_str = ",".join(image_urls)

        # Pricing & carats
        carat_val = item['carat']
        c_pricing = carat_options_map.get(carat_val, (1350, [3, 5, 6], ["1.00 CT", "1.50 CT (+450 USD)", "2.00 CT (+950 USD)"]))
        base_price = c_pricing[0]
        carat_ids = c_pricing[1]
        carat_options_str = "|".join(c_pricing[2])

        # Generate slug
        slug_base = re.sub(r'[^a-z0-9\-]', '-', item['title'].lower()).strip('-')
        slug_base = re.sub(r'\-+', '-', slug_base)[:60]
        unique_slug = f"{slug_base}-{sku.lower()}"

        try:
            # 1. Insert Products
            cur.execute("""
                INSERT INTO products (title, slug, price, category_id, diamond_shape_id, created_at, image_path, is_active)
                VALUES (%s, %s, %s, %s, %s, NOW(), %s, TRUE)
                RETURNING id;
            """, (item['title'], unique_slug, base_price, item['cat_id'], item['shape_id'], main_img))
            prod_id = cur.fetchone()[0]

            # 2. Insert Product Images
            for ord_idx, img_url in enumerate(image_urls, 1):
                cur.execute("""
                    INSERT INTO product_images (product_id, image_path, display_order)
                    VALUES (%s, %s, %s);
                """, (prod_id, img_url, ord_idx))

            # 3. Insert Product Variants
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
                    sku_variant_str = f"SAT-{prod_id}-{m_id}-{c_id}-{sku_idx}"
                    sku_idx += 1
                    cur.execute("""
                        INSERT INTO product_variants (product_id, metal_id, carat_id, sku, price, stock_quantity, is_available)
                        VALUES (%s, %s, %s, %s, %s, 25, TRUE);
                    """, (prod_id, m_id, c_id, sku_variant_str, var_price))

            # 4. Insert CatalogItems
            catalog_id = f"sat-prod-{prod_id}"
            spec_str = f"14K Gold | {carat_val}ct GIA Certified Lab Diamond | {item['title']}"
            cur.execute("""
                INSERT INTO "CatalogItems" ("Id", "Name", "CategoryId", "Spec", "PriceUSD", "Price", "ImageUrl", "GalleryImages", "MetalOptions", "CaratOptions", "IsActive", "CreatedAt")
                VALUES (%s, %s, %s, %s, %s, %s, %s, %s, %s, %s, TRUE, NOW());
            """, (catalog_id, item['title'], str(item['cat_id']), spec_str, base_price, base_price, main_img, gallery_str, metal_options_str, carat_options_str))

            conn.commit()
            total_inserted += 1
            completed_skus.add(sku)
            completed_skus.add(unique_key)

            # Persist checkpoint
            with open(STATE_FILE, 'w') as f:
                json.dump({'completed_skus': sorted(list(completed_skus))}, f, indent=2)

            print(f"  -> SUCCESS! Ingested Product ID {prod_id} with {len(image_urls)} gallery images.")

        except Exception as e:
            conn.rollback()
            print(f"  -> FAILED to ingest {sku}: {e}")

    cur.close()
    conn.close()

    print("\n=====================================================")
    print(f"  BATCH INGESTION COMPLETE! Total Inserted: {total_inserted}")
    print("=====================================================")

if __name__ == '__main__':
    main()
