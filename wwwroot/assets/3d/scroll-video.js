/**
 * Dedicated 3D Background Video Scroll Scrub Controller
 * Smoothly synchronizes video.currentTime with scroll position using requestAnimationFrame & Lerp
 */
(function () {
  'use strict';

  function initVideoScroller() {
    // 1. Video Element Identification
    const video = document.querySelector('.stage video') ||
      document.querySelector('video[src*="experience"]') ||
      document.querySelector('#top video') ||
      document.querySelector('.hero video') ||
      document.querySelector('video');

    if (!video) {
      // Retry in next frame if DOM / React is still mounting
      requestAnimationFrame(initVideoScroller);
      return;
    }

    // Strictly enforce required video attributes & properties
    video.muted = true;
    video.defaultMuted = true;
    video.playsInline = true;
    video.loop = false;
    video.autoplay = false;

    video.setAttribute('playsinline', '');
    video.setAttribute('muted', '');
    video.setAttribute('preload', 'auto');
    video.removeAttribute('autoplay');
    video.removeAttribute('loop');

    try {
      video.pause();
    } catch (e) { }

    const container = document.querySelector('#top') ||
      document.querySelector('.hero') ||
      document.documentElement;

    let targetTime = 0;
    let currentTime = 0;
    let isDurationReady = false;

    function onMetadata() {
      if (video.duration && !isNaN(video.duration) && video.duration > 0) {
        isDurationReady = true;
        updateTargetTime();
      }
    }

    if (video.readyState >= 1 && video.duration && !isNaN(video.duration) && video.duration > 0) {
      isDurationReady = true;
    } else {
      video.addEventListener('loadedmetadata', onMetadata);
      video.addEventListener('loadeddata', onMetadata);
      video.addEventListener('canplay', onMetadata);
      video.addEventListener('durationchange', onMetadata);
      try {
        video.load();
      } catch (e) { }
    }

    function calculateScrollProgress() {
      const totalScrollHeight = container.offsetHeight || container.scrollHeight || (document.documentElement.scrollHeight - window.innerHeight);
      const maxScroll = totalScrollHeight - window.innerHeight;
      if (maxScroll <= 0) return 0;

      const rect = container.getBoundingClientRect();
      const containerTop = window.scrollY + rect.top;
      const scrolled = window.scrollY - containerTop;
      const progress = scrolled / maxScroll;

      return Math.max(0, Math.min(1, progress));
    }

    function updateTargetTime() {
      const duration = video.duration || 0;
      if (duration > 0) {
        const progress = calculateScrollProgress();
        targetTime = progress * duration;
      }
    }

    window.addEventListener('scroll', updateTargetTime, { passive: true });
    window.addEventListener('resize', updateTargetTime, { passive: true });
    updateTargetTime();

    // Smooth Linear Interpolation (lerp) loop
    const LERP_FACTOR = 0.12;

    function renderScrubLoop() {
      const duration = video.duration || 0;
      if (duration > 0) {
        // Interpolate toward target time frame-by-frame
        currentTime += (targetTime - currentTime) * LERP_FACTOR;

        if (Math.abs(targetTime - currentTime) < 0.0008) {
          currentTime = targetTime;
        }

        const boundedTime = Math.max(0, Math.min(duration - 0.001, currentTime));

        if (Math.abs(video.currentTime - boundedTime) > 0.002) {
          try {
            if (typeof video.fastSeek === 'function') {
              video.fastSeek(boundedTime);
            } else {
              video.currentTime = boundedTime;
            }
          } catch (err) {
            try {
              video.currentTime = boundedTime;
            } catch (e) { }
          }
        }
      }

      requestAnimationFrame(renderScrubLoop);
    }

    requestAnimationFrame(renderScrubLoop);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initVideoScroller);
  } else {
    initVideoScroller();
  }
})();
