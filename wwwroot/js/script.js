/* 
  SAT Jewel — Interactive Script Engine (Exclusively USD ($) Transactions)
  Includes FarmBridge-Style Admin Portal & 3D Diamond Intelligence
*/

// Currency Configuration — Exclusively USD ($)
const currentCurrency = 'USD';
const currencyConfig = { symbol: '$', name: 'US Dollar', flag: '🇺🇸' };
let isAdminLoggedIn = false;

// Dynamic Catalog Data Structure (Populated 100% dynamically from Neon PostgreSQL Database via API)
let collectionData = {};

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
  initDynamicStorefront();

  // Check query parameter to auto-open category modal (e.g., from Breadcrumb clicks)
  const urlParams = new URLSearchParams(window.location.search);
  const catToOpen = urlParams.get('openCategory');
  if (catToOpen) {
    setTimeout(() => {
      openCollectionModal(catToOpen);
    }, 400);
  }
});

// Fetch dynamic categories and products from Neon PostgreSQL DB for landing page grid
async function initDynamicStorefront() {
  try {
    const res = await fetch('/api/catalogapi/full-store');
    if (!res.ok) return;
    const rawStoreData = await res.json();

    if (!rawStoreData || rawStoreData.length === 0) return;

    // Filter ONLY active categories (IsActive == true)
    const activeCategories = rawStoreData.filter(c => c.isActive !== false);

    activeCategories.forEach(cat => {
      collectionData[cat.id] = (cat.products || []).map(p => ({
        id: p.id,
        name: p.name,
        spec: p.spec,
        priceUSD: p.priceUSD,
        img: p.imageUrl
      }));
    });

    const grid = document.getElementById('mainCategoryGrid');
    if (!grid) return;

    grid.innerHTML = activeCategories.map((c, idx) => `
      <div class="collection-card reveal" style="transition-delay:${idx * 0.1}s" onclick="openCollectionModal('${c.id}')">
        <div class="collection-img-wrap">
          <img src="${c.imageUrl}" alt="${c.name}" />
          <span class="collection-card-badge">${c.badge}</span>
        </div>
        <div class="collection-body">
          <div class="collection-title">${c.name}</div>
          <div class="collection-count">${(c.products || []).length} Listed Designs | ${c.subtitle}</div>
          <div class="btn-explore-collection">
            Explore ${c.name} Listing <i class="fa-solid fa-arrow-right"></i>
          </div>
        </div>
      </div>
    `).join('');

    initScrollReveals();
  } catch (err) {
    console.warn('API Storefront fetch fallback:', err);
  }
}

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

  // On mobile screens or subsequent visits, instantly skip intro overlay
  const isMobile = window.innerWidth <= 768;
  const isFirstVisit = !sessionStorage.getItem('sat_visited');

  if (isMobile || !isFirstVisit) {
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

  // Hard safety timeout to guarantee overlay NEVER sticks
  const safetyTimer = setTimeout(() => {
    if (overlay) {
      overlay.style.display = 'none';
      overlay.classList.add('fade-out');
    }
    if (logoContainer && !logoContainer.classList.contains('nav-landed')) {
      navSlot.appendChild(logoContainer);
      logoContainer.classList.add('nav-landed');
    }
  }, 1000);

  setTimeout(() => {
    const logoRect = logoContainer.getBoundingClientRect();
    const slotRect = navSlot.getBoundingClientRect();

    const deltaX = slotRect.left - (window.innerWidth / 2 - logoRect.width / 2);
    const deltaY = slotRect.top + (slotRect.height / 2) - (window.innerHeight / 2);
    const scale = slotRect.height / logoRect.height;

    logoContainer.style.transform = `translate(${deltaX}px, ${deltaY}px) scale(${scale * 0.9})`;

    if (overlay) {
      overlay.classList.add('fade-out');
      setTimeout(() => { overlay.style.display = 'none'; }, 600);
    }

    setTimeout(() => {
      clearTimeout(safetyTimer);
      navSlot.appendChild(logoContainer);
      logoContainer.classList.add('nav-landed');
      logoContainer.classList.remove('intro-animating');
      logoContainer.style.transform = '';
    }, 600);

  }, 600);
}

// 2. Navbar Scroll Shrink & Indicator Logic
function initNavbar() {
  const navbar = document.getElementById('navbar');
  const links = document.querySelectorAll('.nav-link');
  const indicator = document.querySelector('.nav-indicator');

  window.addEventListener('scroll', () => {
    if (navbar) {
      if (window.scrollY > 40) {
        navbar.classList.add('scrolled');
      } else {
        navbar.classList.remove('scrolled');
      }
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
    const container = indicator.parentElement;
    if (!container) return;
    const rect = el.getBoundingClientRect();
    const containerRect = container.getBoundingClientRect();
    indicator.style.width = `${rect.width}px`;
    indicator.style.left = `${rect.left - containerRect.left}px`;
    indicator.style.opacity = '1';
  }

  function updateNavIndicator() {
    const scrollPos = window.scrollY + 180;
    let activeFound = false;

    const hashLinks = Array.from(links).filter(l => {
      const href = l.getAttribute('href');
      return href && href.startsWith('#');
    });

    if (window.scrollY <= 50) {
      const homeLink = document.querySelector('.nav-link[href="#hero"]');
      if (homeLink) {
        links.forEach(l => l.classList.remove('active'));
        homeLink.classList.add('active');
        moveIndicator(homeLink);
        return;
      }
    }

    for (let i = hashLinks.length - 1; i >= 0; i--) {
      const link = hashLinks[i];
      const href = link.getAttribute('href');
      const section = document.querySelector(href);

      if (section) {
        const sectionTop = section.getBoundingClientRect().top + window.scrollY;
        if (scrollPos >= sectionTop - 100) {
          links.forEach(l => l.classList.remove('active'));
          link.classList.add('active');
          moveIndicator(link);
          activeFound = true;
          break;
        }
      }
    }
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
    <div class="product-card" onclick="window.location.href='/Product/Details/${p.id}'" style="cursor:pointer;">
      <img src="${p.img}" class="product-thumb" alt="${p.name}" />
      <div class="product-name">${p.name}</div>
      <div class="product-spec">${p.spec}</div>
      <div class="product-price">${formatPrice(p.priceUSD)}</div>
      <button class="btn-product-buy" onclick="event.stopPropagation(); window.location.href='/Product/Details/${p.id}'">
        <i class="fa-solid fa-bag-shopping"></i> Buy Now
      </button>
    </div>
  `).join('');

  modal.classList.add('show');
}

function closeCollectionModal() {
  const modal = document.getElementById('collectionModal');
  if (modal) modal.classList.remove('show');
}

// Global Modal Backdrop Click Handler (Closes modal when clicking anywhere outside)
function handleModalBackdropClick(event, modalId) {
  if (event.target.id === modalId || event.target.classList.contains('collection-modal')) {
    const modal = document.getElementById(modalId) || event.target;
    if (modal) modal.classList.remove('show');
  }
}

// Global Escape Key & Window Outside Click Listeners
window.addEventListener('keydown', (e) => {
  if (e.key === 'Escape') {
    ['collectionModal', 'checkoutModal', 'loginModal'].forEach(id => {
      const modal = document.getElementById(id);
      if (modal) modal.classList.remove('show');
    });
  }
});

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

async function handleAddNewProduct(event) {
  event.preventDefault();

  const categoryId = document.getElementById('adminProdCat').value;
  const name = document.getElementById('adminProdName').value.trim();
  const spec = document.getElementById('adminProdSpec').value.trim();
  const priceUSD = parseFloat(document.getElementById('adminProdPriceUSD').value) || 0;
  const imgSelect = document.getElementById('adminProdImgSelect').value;
  const customImg = document.getElementById('adminProdImgUrl').value;

  const finalImg = customImg.trim() !== '' ? customImg.trim() : imgSelect;

  const newItem = {
    id: `item_${Date.now()}`,
    name: name,
    categoryId: categoryId,
    spec: spec,
    priceUSD: priceUSD,
    imageUrl: finalImg,
    isActive: true
  };

  try {
    const res = await fetch('/api/catalogapi/items', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(newItem)
    });
    const data = await res.json();

    if (res.ok) {
      alert(`✨ Success! "${name}" published to ${categoryId.toUpperCase()} collection in Neon DB!\nLive on public storefront.`);
      const form = document.getElementById('adminAddProductForm');
      if (form) form.reset();
      await initDynamicStorefront();
      refreshFbAdminCatalogTable();
    } else {
      alert(`❌ ${data.message || 'Error publishing item.'}`);
    }
  } catch (err) {
    console.error('Post item error:', err);
    alert(`❌ Server Connection Error: Failed to publish item.`);
  }
}

async function deleteAdminItem(category, id) {
  if (!confirm('Are you sure you want to remove this item from live store?')) return;

  try {
    const res = await fetch(`/api/catalogapi/items/${id}`, { method: 'DELETE' });
    if (res.ok) {
      await initDynamicStorefront();
      refreshFbAdminCatalogTable();
    }
  } catch (err) {
    console.error('Delete item error:', err);
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

// 12. Global SAT Bespoke Lottie Diamond Loading Engine
function showLottieLoader(text) {
  const loader = document.getElementById('satLottieLoader');
  const textEl = document.querySelector('.sat-lottie-text');
  if (text && textEl) {
    textEl.innerHTML = `<span class="sat-gold-sparkle"><i class="fa-solid fa-gem"></i></span> ${text}`;
  }
  if (loader) loader.classList.add('active');
}

function hideLottieLoader() {
  const loader = document.getElementById('satLottieLoader');
  if (loader) loader.classList.remove('active');
}

// Global Payment Success Lottie Trigger
function showPaymentSuccessModal(orderId) {
  hideLottieLoader();
  const modal = document.getElementById('satPaymentSuccessModal');
  const orderIdEl = document.getElementById('successOrderIdDisplay');
  const player = document.getElementById('paymentSuccessLottiePlayer');

  if (orderIdEl && orderId) {
    orderIdEl.textContent = `Order ID: ${orderId}`;
  }
  if (player && typeof player.stop === 'function' && typeof player.play === 'function') {
    player.stop();
    player.play();
  }
  if (modal) {
    modal.classList.add('active');
  }
}

// Global Click Interceptor for Subcategory, Product & Shape Navigation
document.addEventListener('click', (e) => {
  const link = e.target.closest('a');
  if (link && link.href && !link.target && !link.getAttribute('href').startsWith('#') && !link.getAttribute('href').startsWith('javascript:')) {
    try {
      const url = new URL(link.href, window.location.origin);
      const path = url.pathname.toLowerCase();
      
      if (path.includes('/product/category') || path.includes('/product/details') || path === '/product') {
        showLottieLoader('Loading Bespoke Collection...');
      }
    } catch (err) {}
  }
});

window.addEventListener('pageshow', () => {
  hideLottieLoader();
});
