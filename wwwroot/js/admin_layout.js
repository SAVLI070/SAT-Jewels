/* 
  SAT Jewel — Interactive Script Engine (Exclusively USD ($) Transactions)
  Includes FarmBridge-Style Admin Portal & 3D Diamond Intelligence
*/

// Currency Configuration — Exclusively USD ($)
const currentCurrency = 'USD';
const currencyConfig = { symbol: '$', name: 'US Dollar', flag: '🇺🇸' };
let isAdminLoggedIn = false;

// Default Catalog Data in USD ($)
const defaultCollectionData = {
  rings: [
    {
      id: "ring_1",
      name: "Royal Solitaire Diamond Ring",
      spec: "18K Gold | 1.5ct GIA VVS1, E Color | Brilliant Cut",
      priceUSD: 2200,
      img: "assets/ring_1.jpg",
      tags: ["Solitaire", "18K Gold", "GIA VVS1"]
    },
    {
      id: "ring_2",
      name: "Halo Cushion Cut Engagement Ring",
      spec: "Platinum 950 | 2.0ct Halo Diamond Setting | IF Clarity",
      priceUSD: 2900,
      img: "https://images.unsplash.com/photo-1605100804763-247f67b3557e?w=800&q=80",
      tags: ["Platinum 950", "Halo", "2.0ct"]
    },
    {
      id: "ring_3",
      name: "Emerald Cut Vintage Gold Band",
      spec: "18K Yellow Gold | 1.8ct Emerald Cut Diamond",
      priceUSD: 2350,
      img: "https://images.unsplash.com/photo-1603561591411-07134e71a2a9?w=800&q=80",
      tags: ["18K Gold", "Emerald Cut", "Vintage"]
    },
    {
      id: "ring_4",
      name: "Pavé Diamond Eternity Ring",
      spec: "18K White Gold | 1.2ct Continuous Micro-Pavé",
      priceUSD: 1450,
      img: "https://images.unsplash.com/photo-1602751584552-8ba73aad10e1?w=800&q=80",
      tags: ["18K Gold", "Pavé", "Eternity"]
    }
  ],
  necklaces: [
    {
      id: "neck_1",
      name: "Imperial Diamond Floral Pendant",
      spec: "18K Yellow Gold | Marquise & Pear Cut Diamonds | 3.5ct",
      priceUSD: 4200,
      img: "assets/necklace_1.jpg",
      tags: ["18K Gold", "Pendant", "Marquise"]
    },
    {
      id: "neck_2",
      name: "Rivière Diamond Solitaire Choker",
      spec: "Platinum 950 | Graduated Round Diamonds | 5.0ct Total",
      priceUSD: 6250,
      img: "https://images.unsplash.com/photo-1599643478518-a784e5dc4c8f?w=800&q=80",
      tags: ["Platinum 950", "Choker", "5.0ct"]
    },
    {
      id: "neck_3",
      name: "Celestial Sapphire & Diamond Lariat",
      spec: "18K White Gold | Royal Blue Sapphire 2.5ct + Diamonds",
      priceUSD: 2000,
      img: "https://images.unsplash.com/photo-1515562141207-7a88fb7ce338?w=800&q=80",
      tags: ["Sapphire", "18K Gold", "Lariat"]
    }
  ],
  earrings: [
    {
      id: "ear_1",
      name: "Chandelier Diamond Drop Earrings",
      spec: "18K Gold | 2.2ct Triple Drop Cascading Diamonds",
      priceUSD: 2500,
      img: "assets/earring_card.jpg",
      tags: ["18K Gold", "Drops", "Chandelier"]
    },
    {
      id: "ear_2",
      name: "Classic Solitaire Diamond Studs",
      spec: "Platinum 950 | 1.0ct Each (2.0ct Total) | GIA Ideal Cut",
      priceUSD: 3150,
      img: "https://images.unsplash.com/photo-1630019852942-f89202989a59?w=800&q=80",
      tags: ["Platinum 950", "Studs", "GIA Ideal"]
    }
  ],
  bracelets: [
    {
      id: "brac_1",
      name: "Classic Diamond Tennis Bracelet",
      spec: "Platinum 950 | 5.0ct Total Weight | Round Brilliant Diamonds",
      priceUSD: 4700,
      img: "assets/bracelet_card.jpg",
      tags: ["Platinum 950", "Tennis", "5.0ct"]
    },
    {
      id: "brac_2",
      name: "Heritage 18K Gold Diamond Bangle",
      spec: "18K Solid Yellow Gold | 2.8ct Channel Set Diamonds",
      priceUSD: 3450,
      img: "https://images.unsplash.com/photo-1611591475155-4286fa2c2e74?w=800&q=80",
      tags: ["18K Gold", "Bangle", "Channel Set"]
    }
  ]
};

let collectionData = loadCatalog();

function loadCatalog() {
  const saved = localStorage.getItem('sat_catalog_usd');
  if (saved) {
    try {
      return JSON.parse(saved);
    } catch (e) {
      console.error('Error parsing saved catalog:', e);
    }
  }
  return JSON.parse(JSON.stringify(defaultCollectionData));
}

function saveCatalog() {
  localStorage.setItem('sat_catalog_usd', JSON.stringify(collectionData));
  updateCategoryCounts();
}

// DOM Ready Handler
document.addEventListener('DOMContentLoaded', () => {
  initLogoTransition();
  initNavbar();
  initTypewriter();
  initHeroSlider();
  initCounters();
  initScrollReveals();
  updateCategoryCounts();
  refreshFbAdminCatalogTable();
  setUsdCurrencyBadge();
});

function setUsdCurrencyBadge() {
  const flagEl = document.getElementById('userFlag');
  const codeEl = document.getElementById('userCurrCode');
  const badgeEl = document.getElementById('locationCurrencyBadge');
  if (flagEl) flagEl.textContent = '🇺🇸';
  if (codeEl) codeEl.textContent = 'USD ($)';
  if (badgeEl) badgeEl.title = 'Global Currency: USD ($)';
}

function formatPrice(priceUSD) {
  const num = typeof priceUSD === 'number' ? priceUSD : parseFloat(priceUSD) || 0;
  return `$${num.toLocaleString('en-US')}`;
}

// Dynamic Category Counts
function updateCategoryCounts() {
  const counts = {
    rings: (collectionData.rings || []).length,
    necklaces: (collectionData.necklaces || []).length,
    earrings: (collectionData.earrings || []).length,
    bracelets: (collectionData.bracelets || []).length
  };

  const elRings = document.getElementById('count-rings');
  const elNeck = document.getElementById('count-necklaces');
  const elEar = document.getElementById('count-earrings');
  const elBrac = document.getElementById('count-bracelets');

  if (elRings) elRings.textContent = `${counts.rings} Listed Designs | Solitaires & Halos`;
  if (elNeck) elNeck.textContent = `${counts.necklaces} Listed Designs | Chokers & Pendants`;
  if (elEar) elEar.textContent = `${counts.earrings} Listed Designs | Studs & Drops`;
  if (elBrac) elBrac.textContent = `${counts.bracelets} Listed Designs | Tennis & Bangles`;

  const totalItems = counts.rings + counts.necklaces + counts.earrings + counts.bracelets;
  const adminTotalEl = document.getElementById('fbKpiItems');
  if (adminTotalEl) adminTotalEl.textContent = totalItems;
}

// 1. Logo Morph Animation for First-Time Visitors
function initLogoTransition() {
  const logoContainer = document.getElementById('intro-logo-container');
  const navSlot = document.querySelector('.nav-logo-slot');
  const overlay = document.getElementById('intro-overlay');

  if (!logoContainer || !navSlot) return;

  const isFirstVisit = !sessionStorage.getItem('sat_visited');

  if (!isFirstVisit) {
    if (overlay) {
      overlay.style.display = 'none';
      overlay.classList.add('fade-out');
    }
    navSlot.appendChild(logoContainer);
    logoContainer.classList.add('nav-landed');
    return;
  }

  sessionStorage.setItem('sat_visited', 'true');
  logoContainer.classList.add('intro-animating');

  setTimeout(() => {
    const logoRect = logoContainer.getBoundingClientRect();
    const slotRect = navSlot.getBoundingClientRect();

    const deltaX = slotRect.left - (window.innerWidth / 2 - logoRect.width / 2);
    const deltaY = slotRect.top + (slotRect.height / 2) - (window.innerHeight / 2);
    const scale = slotRect.height / logoRect.height;

    logoContainer.style.transform = `translate(${deltaX}px, ${deltaY}px) scale(${scale * 0.9})`;

    if (overlay) overlay.classList.add('fade-out');

    setTimeout(() => {
      navSlot.appendChild(logoContainer);
      logoContainer.classList.add('nav-landed');
      logoContainer.classList.remove('intro-animating');
      logoContainer.style.transform = '';
    }, 1200);

  }, 1400);
}

// 2. Navbar Scroll Shrink & Indicator Logic
function initNavbar() {
  const navbar = document.getElementById('navbar');
  const links = document.querySelectorAll('.nav-link');
  const indicator = document.querySelector('.nav-indicator');

  window.addEventListener('scroll', () => {
    if (window.scrollY > 40) {
      navbar.classList.add('scrolled');
    } else {
      navbar.classList.remove('scrolled');
    }
    updateNavIndicator();
  });

  links.forEach(link => {
    link.addEventListener('click', function(e) {
      links.forEach(l => l.classList.remove('active'));
      this.classList.add('active');
      moveIndicator(this);
    });
  });

  function moveIndicator(el) {
    if (!indicator || !el) return;
    const rect = el.getBoundingClientRect();
    const parentRect = el.parentElement.getBoundingClientRect();
    indicator.style.width = `${rect.width}px`;
    indicator.style.left = `${rect.left - parentRect.left}px`;
    indicator.style.opacity = '1';
  }

  function updateNavIndicator() {
    const fromTop = window.scrollY + 100;
    links.forEach(link => {
      const section = document.querySelector(link.getAttribute('href'));
      if (section) {
        if (section.offsetTop <= fromTop && section.offsetTop + section.offsetHeight > fromTop) {
          links.forEach(l => l.classList.remove('active'));
          link.classList.add('active');
          moveIndicator(link);
        }
      }
    });
  }

  const activeLink = document.querySelector('.nav-link.active');
  if (activeLink) {
    setTimeout(() => moveIndicator(activeLink), 400);
  }
}

// 3. Typewriter Effect
function initTypewriter() {
  const line1El = document.getElementById('typewriter-line1');
  const line2El = document.getElementById('typewriter-line2');
  if (!line1El || !line2El) return;

  const phrase1 = "Mastery in Every Cut,";
  const phrase2 = "Elegance in Every Carat.";
  let i = 0;
  let j = 0;

  function typeLine1() {
    if (i < phrase1.length) {
      line1El.textContent += phrase1.charAt(i);
      i++;
      setTimeout(typeLine1, 45);
    } else {
      setTimeout(typeLine2, 250);
    }
  }

  function typeLine2() {
    if (j < phrase2.length) {
      line2El.textContent += phrase2.charAt(j);
      j++;
      setTimeout(typeLine2, 55);
    }
  }

  setTimeout(typeLine1, 1200);
}

// 4. Hero Background Slider
function initHeroSlider() {
  const images = document.querySelectorAll('.hero-slider img');
  if (images.length === 0) return;
  let currentIdx = 0;

  setInterval(() => {
    images[currentIdx].classList.remove('active');
    currentIdx = (currentIdx + 1) % images.length;
    images[currentIdx].classList.add('active');
  }, 6000);
}

// 5. Animated Number Counters
function initCounters() {
  const counters = document.querySelectorAll('.stat-num');
  let animated = false;

  const observer = new IntersectionObserver((entries) => {
    entries.forEach(entry => {
      if (entry.isIntersecting && !animated) {
        animated = true;
        counters.forEach(stat => {
          const target = parseInt(stat.getAttribute('data-target'), 10) || 0;
          const prefix = stat.getAttribute('data-prefix') || '';
          const suffix = stat.getAttribute('data-suffix') || '';
          const duration = 2000;
          const stepTime = 30;
          const steps = duration / stepTime;
          const increment = target / steps;
          let current = 0;

          const timer = setInterval(() => {
            current += increment;
            if (current >= target) {
              current = target;
              clearInterval(timer);
            }
            stat.innerHTML = `${prefix}${Math.floor(current).toLocaleString()}<span class="stat-unit">${suffix}</span>`;
          }, stepTime);
        });
      }
    });
  }, { threshold: 0.4 });

  const statsSection = document.getElementById('stats');
  if (statsSection) observer.observe(statsSection);
}

// 6. Scroll Reveal Observer
function initScrollReveals() {
  const reveals = document.querySelectorAll('.reveal, .reveal-left, .reveal-right');
  const observer = new IntersectionObserver((entries) => {
    entries.forEach(entry => {
      if (entry.isIntersecting) {
        entry.target.classList.add('active');
      }
    });
  }, { threshold: 0.15 });

  reveals.forEach(el => observer.observe(el));
}

// 7. Collection Listing Modal Logic (USD Pricing)
function openCollectionModal(category) {
  const items = collectionData[category] || collectionData['rings'];
  const modal = document.getElementById('collectionModal');
  const title = document.getElementById('modalTitle');
  const subtitle = document.getElementById('modalSubtitle');
  const grid = document.getElementById('productGrid');

  if (!modal || !title || !grid) return;

  modal.setAttribute('data-category', category);

  const categoryTitles = {
    rings: "Most Selling Rings Collection",
    necklaces: "Most Selling Necklaces & Pendants",
    earrings: "Most Selling Earrings Collection",
    bracelets: "Most Selling Bracelets & Bangles"
  };

  title.textContent = categoryTitles[category] || "Jewelry Collection";
  subtitle.textContent = `Displaying ${items.length} GIA-certified designs in USD ($).`;

  grid.innerHTML = items.map(p => `
    <div class="product-card">
      <img src="${p.img}" class="product-thumb" alt="${p.name}" />
      <div class="product-name">${p.name}</div>
      <div class="product-spec">${p.spec}</div>
      <div class="product-price">${formatPrice(p.priceUSD)}</div>
      <button class="btn-product-inquire" onclick="openCheckoutModal('${p.name}', ${p.priceUSD}, '${p.img}')">
        <i class="fa-solid fa-credit-card"></i> Instant Checkout (USD)
      </button>
    </div>
  `).join('');

  modal.classList.add('show');
}

function closeCollectionModal() {
  const modal = document.getElementById('collectionModal');
  if (modal) modal.classList.remove('show');
}

// 8. Instant USD Checkout & Payment Modal Logic
let activeCheckoutProduct = null;

function openCheckoutModal(name, priceUSD, img) {
  activeCheckoutProduct = { name, priceUSD, img };
  const modal = document.getElementById('checkoutModal');
  if (!modal) return;

  document.getElementById('checkoutItemTitle').textContent = name;
  document.getElementById('checkoutItemImg').src = img;
  
  const formatted = formatPrice(priceUSD);
  document.getElementById('checkoutItemPriceUSD').textContent = formatted;

  modal.classList.add('show');
}

function closeCheckoutModal() {
  const modal = document.getElementById('checkoutModal');
  if (modal) modal.classList.remove('show');
}

function processPayment(method) {
  if (!activeCheckoutProduct) return;
  const formatted = formatPrice(activeCheckoutProduct.priceUSD);
  alert(`Payment Initiated via ${method.toUpperCase()} in USD!\nItem: ${activeCheckoutProduct.name}\nTotal Amount: ${formatted} USD\n\nThank you for choosing SAT Jewel. Your GIA certificate & insured order are being processed.`);
  closeCheckoutModal();
}

// 9. CLIENT PORTAL AUTHENTICATION & LOGIN MODAL
function openClientPortalLogin(e) {
  if (e) e.preventDefault();
  
  if (isAdminLoggedIn) {
    openFbAdminPortal();
  } else {
    const loginModal = document.getElementById('loginModal');
    if (loginModal) {
      document.getElementById('loginErrorMsg').style.display = 'none';
      document.getElementById('adminLoginForm').reset();
      loginModal.classList.add('show');
    }
  }
}

function closeLoginModal() {
  const modal = document.getElementById('loginModal');
  if (modal) modal.classList.remove('show');
}

function handleAdminLogin(e) {
  e.preventDefault();
  const user = document.getElementById('loginUser').value.trim();
  const pass = document.getElementById('loginPass').value.trim();
  const errEl = document.getElementById('loginErrorMsg');

  if ((user === 'admin' || user.includes('@')) && (pass === 'admin123' || pass === 'admin' || pass === 'sat2026')) {
    isAdminLoggedIn = true;
    closeLoginModal();
    openFbAdminPortal();
  } else {
    errEl.textContent = '❌ Invalid Credentials! Use User: admin | Pass: admin123';
    errEl.style.display = 'block';
  }
}

// 10. FARMBRIDGE-STYLE FULL ADMIN DASHBOARD PORTAL
function openFbAdminPortal() {
  const portal = document.getElementById('farmbridgeAdminPortal');
  if (portal) {
    refreshFbAdminCatalogTable();
    portal.classList.add('show');
  }
}

function closeFbAdminPortal() {
  const portal = document.getElementById('farmbridgeAdminPortal');
  if (portal) portal.classList.remove('show');
}

function showFbAdminPage(pageName) {
  document.querySelectorAll('.adm-nav-link').forEach(link => link.classList.remove('active'));
  document.querySelectorAll('.adm-page-section').forEach(sec => sec.classList.remove('active'));

  const navItem = document.getElementById(`admNav-${pageName}`);
  const secItem = document.getElementById(`admSec-${pageName}`);
  const titleEl = document.getElementById('admPageTitle');

  if (navItem) navItem.classList.add('active');
  if (secItem) secItem.classList.add('active');

  const pageTitles = {
    overview: "Dashboard Overview",
    catalog: "Jewelry Catalog Management",
    add: "Publish New Collection Item",
    market: "Market Intelligence & RAPAPORT Index"
  };

  if (titleEl) titleEl.textContent = pageTitles[pageName] || "Admin Dashboard";
}

function toggleAdmDrawer(e) {
  if (e) e.stopPropagation();
  const sidebar = document.getElementById('admSidebar');
  const backdrop = document.getElementById('admBackdrop');

  if (!sidebar) return;

  if (window.innerWidth <= 900) {
    sidebar.classList.toggle('open');
    if (backdrop) backdrop.classList.toggle('show');
  } else {
    sidebar.classList.toggle('collapsed');
  }
}

function closeAdmOnMobile() {
  if (window.innerWidth <= 900) {
    const sidebar = document.getElementById('admSidebar');
    const backdrop = document.getElementById('admBackdrop');
    if (sidebar) sidebar.classList.remove('open');
    if (backdrop) backdrop.classList.remove('show');
  }
}

function changeRevPeriod(period, btn) {
  if (btn) {
    const parent = btn.parentElement;
    parent.querySelectorAll('.btn-pd').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
  }

  const revEl = document.getElementById('fbRevVal');
  const revSubEl = document.getElementById('fbRevSub');

  if (period === 'weekly') {
    if (revEl) revEl.textContent = '$35,000';
    if (revSubEl) revSubEl.textContent = "This week's gross earnings (USD)";
  } else if (period === 'monthly') {
    if (revEl) revEl.textContent = '$140,000';
    if (revSubEl) revSubEl.textContent = "This month's gross earnings (USD)";
  } else if (period === 'yearly') {
    if (revEl) revEl.textContent = '$1,680,000';
    if (revSubEl) revSubEl.textContent = "Annual gross earnings (USD)";
  }
}

// Refresh FarmBridge Admin Catalog Data Table (USD)
function refreshFbAdminCatalogTable() {
  const tbody = document.getElementById('fbAdminCatalogBody');
  if (!tbody) return;

  let html = '';
  let totalCount = 0;

  Object.keys(collectionData).forEach(cat => {
    collectionData[cat].forEach(item => {
      totalCount++;
      const priceFormatted = formatPrice(item.priceUSD);
      html += `
        <tr>
          <td><img src="${item.img}" style="width:42px;height:42px;border-radius:8px;object-fit:cover;" /></td>
          <td><strong>${item.name}</strong><br/><small style="color:#64748b;">${item.spec}</small></td>
          <td><span class="badge-cat">${cat.toUpperCase()}</span></td>
          <td><strong style="color:#b45309;">${priceFormatted}</strong></td>
          <td><span class="badge-stock">In Stock</span></td>
          <td>
            <button class="btn-table-delete" onclick="deleteAdminItem('${cat}', '${item.id}')">
              <i class="fa-solid fa-trash"></i> Delete
            </button>
          </td>
        </tr>
      `;
    });
  });

  tbody.innerHTML = html;
  const countEl = document.getElementById('fbKpiItems');
  if (countEl) countEl.textContent = totalCount;
  updateCategoryCounts();
}

// Global ElasticSearch Filter for Admin Dashboard
function handleFbAdminSearch() {
  const query = document.getElementById('globalSearchInput').value.toLowerCase().trim();
  const resultsDiv = document.getElementById('globalSearchResults');
  const listDiv = document.getElementById('searchResultsList');

  if (query === '') {
    if (resultsDiv) resultsDiv.style.display = 'none';
    return;
  }

  let matches = [];
  Object.keys(collectionData).forEach(cat => {
    collectionData[cat].forEach(item => {
      if (item.name.toLowerCase().includes(query) || item.spec.toLowerCase().includes(query) || cat.includes(query)) {
        matches.push({ ...item, category: cat });
      }
    });
  });

  if (listDiv) {
    if (matches.length > 0) {
      listDiv.innerHTML = matches.map(m => `
        <div style="padding:8px;border-bottom:1px solid #e2e8f0;display:flex;align-items:center;justify-content:space-between;">
          <div>
            <strong>${m.name}</strong> (${m.category.toUpperCase()})<br/>
            <small style="color:#64748b;">${m.spec}</small>
          </div>
          <span style="color:#b45309;font-weight:700;">${formatPrice(m.priceUSD)}</span>
        </div>
      `).join('');
    } else {
      listDiv.innerHTML = `<div style="padding:12px;color:#64748b;">No items matched "${query}".</div>`;
    }
  }

  if (resultsDiv) resultsDiv.style.display = 'block';
}

function handleAddNewProduct(event) {
  event.preventDefault();

  const category = document.getElementById('adminProdCat').value;
  const name = document.getElementById('adminProdName').value;
  const spec = document.getElementById('adminProdSpec').value;
  const priceUSD = parseFloat(document.getElementById('adminProdPriceUSD').value) || 0;
  const imgSelect = document.getElementById('adminProdImgSelect').value;
  const customImg = document.getElementById('adminProdImgUrl').value;

  const finalImg = customImg.trim() !== '' ? customImg.trim() : imgSelect;

  const newItem = {
    id: `item_${Date.now()}`,
    name: name,
    spec: spec,
    priceUSD: priceUSD,
    img: finalImg
  };

  collectionData[category].unshift(newItem);
  saveCatalog();

  alert(`✨ Success! "${name}" published to ${category.toUpperCase()} collection for ${formatPrice(priceUSD)} USD.\nLive on storefront & database!`);

  document.getElementById('adminAddProductForm').reset();
  showFbAdminPage('catalog');
  refreshFbAdminCatalogTable();

  const modal = document.getElementById('collectionModal');
  if (modal && modal.classList.contains('show')) {
    openCollectionModal(category);
  }
}

function deleteAdminItem(category, id) {
  if (confirm('Are you sure you want to remove this item from live store?')) {
    collectionData[category] = collectionData[category].filter(i => i.id !== id);
    saveCatalog();
    refreshFbAdminCatalogTable();
  }
}

function logout() {
  isAdminLoggedIn = false;
  closeFbAdminPortal();
  alert('You have been logged out of SAT Jewel Admin Portal.');
}

// 11. Mobile Menu Drawer Toggle
function toggleMenu() {
  const mobileMenu = document.getElementById('mobileMenu');
  if (mobileMenu) mobileMenu.classList.toggle('active');
}
