// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Dark mode toggle
document.addEventListener('DOMContentLoaded', function () {
    const html = document.getElementById('htmlRoot');
    const btn = document.getElementById('darkModeToggle');
    const icon = document.getElementById('darkModeIcon');

    if (!btn) return;

    function applyTheme(isDark) {
        if (isDark) {
            html.setAttribute('data-bs-theme', 'dark');
            icon.classList.replace('bx-moon', 'bx-sun');
        } else {
            html.removeAttribute('data-bs-theme');
            icon.classList.replace('bx-sun', 'bx-moon');
        }
    }

    // Sync icon with current state set by the inline init script
    applyTheme(html.getAttribute('data-bs-theme') === 'dark');

    btn.addEventListener('click', function () {
        const isDark = html.getAttribute('data-bs-theme') !== 'dark';
        applyTheme(isDark);
        localStorage.setItem('theme', isDark ? 'dark' : 'light');

        // Notify any page-level chart listeners
        document.dispatchEvent(new CustomEvent('themeChanged', { detail: { dark: isDark } }));
    });
});
