(function () {
    // Wird vom Plugin automatisch in die Web-index.html eingehängt.
    // Hover über eine Card -> spielt die vorgenerierte Datei /VideoPreviews/{id} (statisch, sofort, glatt).
    var HOVER_DELAY = 200;

    function token() {
        try { return window.ApiClient && ApiClient.accessToken ? ApiClient.accessToken() : null; } catch (e) { return null; }
    }
    function previewUrl(id) {
        var t = token();
        var path = 'VideoPreviews/' + id + (t ? ('?api_key=' + encodeURIComponent(t)) : '');
        return (window.ApiClient && ApiClient.getUrl) ? ApiClient.getUrl(path) : ('/' + path);
    }

    var overlay = document.createElement('div');
    overlay.style.cssText = 'position:absolute;inset:0;z-index:100;display:none;pointer-events:none;background:#000;overflow:hidden;';
    var vid = document.createElement('video');
    vid.muted = true; vid.playsInline = true; vid.loop = true; vid.preload = 'auto';
    vid.style.cssText = 'position:absolute;inset:0;width:100%;height:100%;object-fit:cover;';
    overlay.appendChild(vid);

    var hoverTimer = null, currentCard = null;

    function clear() {
        if (hoverTimer) { clearTimeout(hoverTimer); hoverTimer = null; }
        try { vid.pause(); } catch (e) {}
        vid.removeAttribute('src');
        try { vid.load(); } catch (e) {}
        overlay.style.display = 'none';
        if (overlay.parentNode) { overlay.remove(); }
    }

    function start(id, target) {
        if (getComputedStyle(target).position === 'static') { target.style.position = 'relative'; }
        target.appendChild(overlay);
        overlay.style.display = 'block';
        vid.src = previewUrl(id);
        vid.play().catch(function () {});
        vid.addEventListener('error', function onerr() { vid.removeEventListener('error', onerr); clear(); }, { once: true });
    }

    document.addEventListener('mouseover', function (e) {
        var card = e.target.closest && e.target.closest('.card');
        if (!card || card === currentCard) { return; }
        var idEl = card.querySelector('[data-id]') || card;
        var id = idEl.getAttribute('data-id') || card.getAttribute('data-id');
        if (!id) { return; }
        currentCard = card;
        var target = card.querySelector('.cardImageContainer') || card.querySelector('.cardBox') || card;
        if (hoverTimer) { clearTimeout(hoverTimer); }
        hoverTimer = setTimeout(function () { start(id, target); }, HOVER_DELAY);
    });

    document.addEventListener('mouseout', function (e) {
        if (currentCard && !currentCard.contains(e.relatedTarget)) { currentCard = null; clear(); }
    });

    var lastUrl = location.href;
    setInterval(function () { if (location.href !== lastUrl) { lastUrl = location.href; currentCard = null; clear(); } }, 250);

    console.log('vidprev: aktiv (Plugin-Vorschauen)');
})();
