/* 
  SAT Jewel — Interactive Script Engine, Automatic IP Location Currency & FarmBridge-Style Admin Portal
*/

// Multi-Currency Exchange Rates relative to INR base
const currencyConfig = {
  INR: { rate: 1, symbol: '₹', flag: '🇮🇳', name: 'Indian Rupee' },
  USD: { rate: 1 / 83.50, symbol: '$', flag: '🇺🇸', name: 'US Dollar' },
  EUR: { rate: 1 / 90.80, symbol: '€', flag: '🇪🇺', name: 'Euro' },
  GBP: { rate: 1 / 106.20, symbol: '£', flag: '🇬🇧', name: 'British Pound' },
  AED: { rate: 1 / 22.74, symbol: 'AED ', flag: '🇦🇪', name: 'UAE Dirham' },
  CAD: { rate: 1 / 61.20, symbol: 'CA$', flag: '🇨🇦', name: 'Canadian Dollar' },
  AUD: { rate: 1 / 54.80, symbol: 'A$', flag: '🇦🇺', name: 'Australian Dollar' }
};

let currentCurrency = 'INR';
let userCountryName = 'India';
let isAdminLoggedIn = false;

// Initial Catalog Data
const defaultCollectionData = {
  rings: [
    {
      id: 'ring_1',
      name: "Royal Solitaire Diamond Ring",
      spec: "18K Gold | 1.5ct GIA VVS1, E Color | Brilliant Cut",
      priceINR: 185000,
      img: "assets/ring_1.jpg"
    },
    {
      id: 'ring_2',
      name: "Emerald Cut Halo Ring",
      spec: "Platinum 950 | 2.0ct GIA VS1 | Pavé Diamond Halo",
      priceINR: 240000,
      img: "https://images.unsplash.com/photo-1605100804763-247f67b3557e?w=600&q=80&fm=jpg"
    },
    {
      id: 'ring_3',
      name: "Sapphire & Diamond Vintage Ring",
      spec: "18K Rose Gold | 1.8ct Natural Sapphire & Diamonds",
      priceINR: 195000,
      img: "assets/hero_2.jpg"
    },
    {
      id: 'ring_4',
      name: "Pavé Diamond Eternity Band",
      spec: "18K White Gold | 1.2ct Total Weight | Round Cut",
      priceINR: 120000,
      img: "assets/ring_1.jpg"
    }
  ],
  necklaces: [
    {
      id: 'neck_1',
      name: "Imperial Diamond Floral Pendant",
      spec: "18K Yellow Gold | Marquise & Pear Cut Diamonds",
      priceINR: 350000,
      img: "assets/necklace_1.jpg"
    },
    {
      id: 'neck_2',
      name: "Solitaire Diamond Y-Necklace",
      spec: "18K White Gold | 1.0ct Drop Solitaire",
      priceINR: 165000,
      img: "assets/hero_1.jpg"
    },
    {
      id: 'neck_3',
      name: "Royal Emerald & Diamond Collar",
      spec: "18K Gold | 4.5ct Colombian Emerald & Pavé",
      priceINR: 520000,
      img: "assets/necklace_1.jpg"
    }
  ],
  earrings: [
    {
      id: 'ear_1',
      name: "Chandelier Diamond Drop Earrings",
      spec: "18K Gold | 2.2ct Triple Drop Diamonds",
      priceINR: 210000,
      img: "assets/earring_card.jpg"
    },
    {
      id: 'ear_2',
      name: "Classic Solitaire Diamond Studs",
      spec: "Platinum 950 | 1.0ct Each (2.0ct t.w.) GIA VVS1",
      priceINR: 260000,
      img: "assets/earring_card.jpg"
    }
  ],
  bracelets: [
    {
      id: 'brac_1',
      name: "Classic Diamond Tennis Bracelet",
      spec: "Platinum 950 | 5.0ct Total Weight | Round Cut",
      priceINR: 390000,
      img: "assets/bracelet_card.jpg"
    },
    {
      id: 'brac_2',
      name: "18K Gold & Diamond Bangle Set",
      spec: "18K Yellow Gold | Hand-Carved Filigree Pattern",
      priceINR: 285000,
      img: "assets/bracelet_card.jpg"
    }
  ]
};

let collectionData = loadCatalog();

function loadCatalog() {
  const saved = localStorage.getItem('sat_catalog');
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
  localStorage.setItem('sat_catalog', JSON.stringify(collectionData));
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
  detectLocationAndSetCurrency();
});

// Automatic IP Location & Currency Detection Engine
async function detectLocationAndSetCurrency() {
  try {
    const res = await fetch('https://ipapi.co/json/');
    const data = await res.json();

    if (data && data.currency) {
      const code = data.currency.toUpperCase();
      userCountryName = data.country_name || 'Your Location';

      if (currencyConfig[code]) {
        currentCurrency = code;
      } else if (code === 'USD' || data.country_code === 'US') {
        currentCurrency = 'USD';
      } else {
        currentCurrency = 'USD';
      }
    }
  } catch (err) {
    console.log('IP Location fetch fallback to timezone:', err);
    const tz = Intl.DateTimeFormat().resolvedOptions().timeZone || '';
    if (tz.includes('Kolkata') || tz.includes('Calcutta') || tz.includes('Asia/Colombo')) {
      currentCurrency = 'INR';
      userCountryName = 'India';
    } else if (tz.includes('Europe/London')) {
      currentCurrency = 'GBP';
      userCountryName = 'United Kingdom';
    } else if (tz.includes('Europe/')) {
      currentCurrency = 'EUR';
      userCountryName = 'Europe';
    } else if (tz.includes('Dubai') || tz.includes('Muscat')) {
      currentCurrency = 'AED';
      userCountryName = 'UAE';
    } else {
      currentCurrency = 'USD';
      userCountryName = 'United States';
    }
  }

  updateLocationBadge();
}

function updateLocationBadge() {
  const config = currencyConfig[currentCurrency] || currencyConfig['USD'];
  const flagEl = document.getElementById('userFlag');
  const codeEl = document.getElementById('userCurrCode');
  const badgeEl = document.getElementById('locationCurrencyBadge');

  if (flagEl) flagEl.textContent = config.flag;
  if (codeEl) codeEl.textContent = `${currentCurrency} (${config.symbol.trim()})`;
  if (badgeEl) badgeEl.title = `Auto-detected location: ${userCountryName} (${currentCurrency})`;

  const openModal = document.getElementById('collectionModal');
  if (openModal && openModal.classList.contains('show')) {
    const activeCategory = openModal.getAttribute('data-category') || 'rings';
    openCollectionModal(activeCategory);
  }
}

function formatPrice(priceINR) {
  const config = currencyConfig[currentCurrency] || currencyConfig['USD'];
  const converted = Math.round(priceINR * config.rate);

  if (currentCurrency === 'INR') {
    return `₹${priceINR.toLocaleString('en-IN')}`;
  }
  return `${config.symbol}${converted.toLocaleString()} ${currentCurrency}`;
}

function updateCategoryCounts() {
  const ringCount = collectionData.rings.length;
  const neckCount = collectionData.necklaces.length;
  const earCount = collectionData.earrings.length;
  const bracCount = collectionData.bracelets.length;

  const ringEl = document.getElementById('count-rings');
  if (ringEl) ringEl.textContent = `${ringCount} Listed Designs | Solitaires & Halos`;

  const neckEl = document.getElementById('count-necklaces');
  if (neckEl) neckEl.textContent = `${neckCount} Listed Designs | Chokers & Pendants`;

  const earEl = document.getElementById('count-earrings');
  if (earEl) earEl.textContent = `${earCount} Listed Designs | Studs & Drops`;

  const bracEl = document.getElementById('count-bracelets');
  if (bracEl) bracEl.textContent = `${bracCount} Listed Designs | Tennis & Bangles`;

  const totalItems = ringCount + neckCount + earCount + bracCount;
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
    // Immediate placement into navbar for repeat visits within session
    if (overlay) {
      overlay.style.display = 'none';
      overlay.classList.add('fade-out');
    }
    navSlot.appendChild(logoContainer);
    logoContainer.classList.add('nav-landed');
    return;
  }

  // Set session flag so intro only plays on first visit
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
    link.addEventListener('click', () => {
      links.forEach(l => l.classList.remove('active'));
      link.classList.add('active');
      updateNavIndicator();
    });
  });

  function updateNavIndicator() {
    const activeLink = document.querySelector('.nav-link.active');
    if (activeLink && indicator) {
      indicator.style.left = activeLink.offsetLeft + 'px';
      indicator.style.width = activeLink.offsetWidth + 'px';
    }
  }

  setTimeout(updateNavIndicator, 300);
}

// 3. Typewriter Effect Logic
function initTypewriter() {
  const line1Element = document.getElementById('typewriter-line1');
  const line2Element = document.getElementById('typewriter-line2');
  if (!line1Element || !line2Element) return;

  const phrasePairs = [
    { l1: "SAT Jewel — Bespoke", l2: "Fine Jewelry & Gold" },
    { l1: "Where Timeless Craft Meets", l2: "GIA Certified Perfection" },
    { l1: "Rare Gemstones &", l2: "AI Diamond Intelligence" }
  ];

  let pairIndex = 0;
  let charIndex = 0;
  let isDeleting = false;
  let isLine1 = true;

  function type() {
    const currentPair = phrasePairs[pairIndex];
    
    if (isLine1) {
      if (!isDeleting) {
        line1Element.textContent = currentPair.l1.substring(0, charIndex + 1);
        charIndex++;
        if (charIndex === currentPair.l1.length) {
          isLine1 = false;
          charIndex = 0;
          setTimeout(type, 300);
          return;
        }
      }
    } else {
      if (!isDeleting) {
        line2Element.textContent = currentPair.l2.substring(0, charIndex + 1);
        charIndex++;
        if (charIndex === currentPair.l2.length) {
          isDeleting = true;
          setTimeout(type, 2500);
          return;
        }
      } else {
        line2Element.textContent = currentPair.l2.substring(0, charIndex - 1);
        charIndex--;
        if (charIndex === 0) {
          line1Element.textContent = "";
          isDeleting = false;
          isLine1 = true;
          pairIndex = (pairIndex + 1) % phrasePairs.length;
          setTimeout(type, 400);
          return;
        }
      }
    }

    const speed = isDeleting ? 40 : 80;
    setTimeout(type, speed);
  }

  type();
}

// 4. Hero Background Slider
function initHeroSlider() {
  const images = document.querySelectorAll('.hero-slider img');
  if (images.length === 0) return;

  let currentIndex = 0;
  setInterval(() => {
    images[currentIndex].classList.remove('active');
    currentIndex = (currentIndex + 1) % images.length;
    images[currentIndex].classList.add('active');
  }, 5000);
}

// 5. Scroll Counters Animation
function initCounters() {
  const statNumbers = document.querySelectorAll('.stat-num');
  let animated = false;

  const observer = new IntersectionObserver((entries) => {
    entries.forEach(entry => {
      if (entry.isIntersecting && !animated) {
        animated = true;
        statNumbers.forEach(stat => {
          const target = parseInt(stat.getAttribute('data-target') || '0', 10);
          const prefix = stat.getAttribute('data-prefix') || '';
          const suffix = stat.getAttribute('data-suffix') || '';
          
          let current = 0;
          const step = Math.max(1, Math.ceil(target / 40));
          const timer = setInterval(() => {
            current += step;
            if (current >= target) {
              current = target;
              clearInterval(timer);
            }
            stat.innerHTML = `${prefix}${current.toLocaleString()}<span class="stat-unit">${suffix}</span>`;
          }, 40);
        });
      }
    });
  }, { threshold: 0.5 });

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

// 7. Collection Listing Modal Logic
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
  subtitle.textContent = `Displaying ${items.length} GIA-certified designs in ${currentCurrency}.`;

  grid.innerHTML = items.map(p => `
    <div class="product-card">
      <img src="${p.img}" class="product-thumb" alt="${p.name}" />
      <div class="product-name">${p.name}</div>
      <div class="product-spec">${p.spec}</div>
      <div class="product-price">${formatPrice(p.priceINR)}</div>
      <button class="btn-product-inquire" onclick="openCheckoutModal('${p.name}', ${p.priceINR}, '${p.img}')">
        <i class="fa-solid fa-credit-card"></i> Buy / Inquire Now
      </button>
    </div>
  `).join('');

  modal.classList.add('show');
}

function closeCollectionModal() {
  const modal = document.getElementById('collectionModal');
  if (modal) modal.classList.remove('show');
}

// 8. Checkout & Payment Modal Logic
let activeCheckoutProduct = null;

function openCheckoutModal(name, priceINR, img) {
  activeCheckoutProduct = { name, priceINR, img };
  const modal = document.getElementById('checkoutModal');
  if (!modal) return;

  document.getElementById('checkoutItemTitle').textContent = name;
  document.getElementById('checkoutItemImg').src = img;
  
  const formatted = formatPrice(priceINR);
  document.getElementById('checkoutItemPriceINR').textContent = formatted;
  document.getElementById('checkoutItemPriceUSD').textContent = `Detected Region: ${userCountryName} (${currentCurrency})`;

  modal.classList.add('show');
}

function closeCheckoutModal() {
  const modal = document.getElementById('checkoutModal');
  if (modal) modal.classList.remove('show');
}

function switchPaymentTab(tab) {
  document.querySelectorAll('.pay-tab-btn').forEach(b => b.classList.remove('active'));
  document.querySelectorAll('.payment-pane').forEach(p => p.classList.remove('active'));

  if (tab === 'usd') {
    document.getElementById('tab-usd-btn').classList.add('active');
    document.getElementById('pane-usd').classList.add('active');
  } else {
    document.getElementById('tab-inr-btn').classList.add('active');
    document.getElementById('pane-inr').classList.add('active');
  }
}

function processPayment(method) {
  if (!activeCheckoutProduct) return;
  const formatted = formatPrice(activeCheckoutProduct.priceINR);
  alert(`Payment Initiated via ${method.toUpperCase()}!\nProduct: ${activeCheckoutProduct.name}\nAmount: ${formatted}\n\nThank you for choosing SAT Jewel. Your concierge will confirm your order.`);
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

function toggleAdmDrawer() {
  const sidebar = document.getElementById('admSidebar');
  if (sidebar) sidebar.classList.toggle('collapsed');
}

function toggleAdmNotif(e) {
  if (e) e.stopPropagation();
  const notif = document.getElementById('admNotif');
  if (notif) notif.classList.toggle('show');
}

function admMarkAllRead() {
  const badge = document.getElementById('admBellBadge');
  if (badge) badge.style.display = 'none';
  alert('All notifications marked as read.');
}

// Period Switcher for Financial Revenue Chart (Weekly / Monthly / Yearly)
function changeRevPeriod(period, btn) {
  if (btn) {
    const parent = btn.parentElement;
    parent.querySelectorAll('.btn-pd').forEach(b => b.classList.remove('active'));
    btn.classList.add('active');
  }

  const revEl = document.getElementById('fbRevVal');
  const revSubEl = document.getElementById('fbRevSub');

  if (period === 'weekly') {
    if (revEl) revEl.textContent = '₹28,50,000';
    if (revSubEl) revSubEl.textContent = "This week's gross earnings";
  } else if (period === 'monthly') {
    if (revEl) revEl.textContent = '₹1,14,000,000';
    if (revSubEl) revSubEl.textContent = "This month's gross earnings";
  } else if (period === 'yearly') {
    if (revEl) revEl.textContent = '₹13,68,000,000';
    if (revSubEl) revSubEl.textContent = "Annual gross earnings";
  }
}

// Refresh FarmBridge Admin Catalog Data Table
function refreshFbAdminCatalogTable() {
  const tbody = document.getElementById('fbAdminCatalogBody');
  if (!tbody) return;

  let html = '';
  let totalCount = 0;

  Object.keys(collectionData).forEach(cat => {
    collectionData[cat].forEach(item => {
      totalCount++;
      const priceFormatted = formatPrice(item.priceINR);
      html += `
        <tr>
          <td><img src="${item.img}" style="width:42px;height:42px;border-radius:8px;object-fit:cover;" /></td>
          <td><strong>${item.name}</strong><br/><small style="color:var(--taupe-beige);">${item.spec}</small></td>
          <td><span class="badge-cat">${cat.toUpperCase()}</span></td>
          <td><strong style="color:var(--champagne-gold);">${priceFormatted}</strong></td>
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
        <div style="padding:8px;border-bottom:1px solid var(--border-glass);display:flex;align-items:center;justify-content:space-between;">
          <div>
            <strong>${m.name}</strong> (${m.category.toUpperCase()})<br/>
            <small style="color:var(--taupe-beige);">${m.spec}</small>
          </div>
          <span style="color:var(--champagne-gold);font-weight:700;">${formatPrice(m.priceINR)}</span>
        </div>
      `).join('');
    } else {
      listDiv.innerHTML = `<div style="padding:12px;color:var(--taupe-beige);">No items matched "${query}".</div>`;
    }
  }

  if (resultsDiv) resultsDiv.style.display = 'block';
}

function handleAddNewProduct(event) {
  event.preventDefault();

  const category = document.getElementById('adminProdCat').value;
  const name = document.getElementById('adminProdName').value;
  const spec = document.getElementById('adminProdSpec').value;
  const priceINR = parseInt(document.getElementById('adminProdPriceINR').value, 10);
  const imgSelect = document.getElementById('adminProdImgSelect').value;
  const customImg = document.getElementById('adminProdImgUrl').value;

  const finalImg = customImg.trim() !== '' ? customImg.trim() : imgSelect;

  const newItem = {
    id: `item_${Date.now()}`,
    name: name,
    spec: spec,
    priceINR: priceINR,
    img: finalImg
  };

  collectionData[category].unshift(newItem);
  saveCatalog();

  alert(`✨ Success! "${name}" published to ${category.toUpperCase()} collection.\nIt is now live on the public storefront!`);

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

function resetCatalogToDefault() {
  if (confirm('Reset catalog to default original items?')) {
    localStorage.removeItem('sat_catalog');
    collectionData = JSON.parse(JSON.stringify(defaultCollectionData));
    saveCatalog();
    refreshFbAdminCatalogTable();
    alert('Catalog reset to default items!');
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
