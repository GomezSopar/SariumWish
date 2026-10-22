(() => {
    const storageKey = 'workspace-theme';
    const root = document.documentElement;

    const preferred = () => {
        const stored = localStorage.getItem(storageKey);
        if (stored === 'light' || stored === 'dark') {
            return stored;
        }
        return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
    };

    const apply = (theme) => root.setAttribute('data-bs-theme', theme);

    apply(preferred());

    window.workspaceTheme = {
        current: () => root.getAttribute('data-bs-theme') ?? 'light',
        toggle: () => {
            const next = window.workspaceTheme.current() === 'dark' ? 'light' : 'dark';
            localStorage.setItem(storageKey, next);
            apply(next);
            return next;
        }
    };
})();
