() => {
    const baseHref = document.baseURI;

    const toAbsolute = (value) => {
        if (!value || /^(data:|blob:|#|javascript:|mailto:|tel:)/i.test(value.trim())) {
            return value;
        }
        try {
            return new URL(value, baseHref).href;
        } catch {
            return value;
        }
    };

    for (const style of document.querySelectorAll('style')) {
        try {
            const rules = style.sheet?.cssRules;
            if (rules && rules.length > 0) {
                const serialized = Array.from(rules, (rule) => rule.cssText).join('\n');
                if (serialized.length > style.textContent.length) {
                    style.textContent = serialized;
                }
            }
        } catch {
        }
    }

    if (document.adoptedStyleSheets?.length) {
        const adopted = document.createElement('style');
        adopted.setAttribute('data-snapshot-adopted', '');
        adopted.textContent = document.adoptedStyleSheets
            .flatMap((sheet) => Array.from(sheet.cssRules, (rule) => rule.cssText))
            .join('\n');
        document.head.appendChild(adopted);
    }

    for (const link of document.querySelectorAll('link[rel~="stylesheet"][media="print"][onload]')) {
        link.setAttribute('media', 'all');
    }

    for (const img of document.querySelectorAll('img')) {
        const lazySource = img.getAttribute('data-src') || img.getAttribute('data-lazy-src') || img.getAttribute('data-original');
        const currentSource = img.currentSrc || img.getAttribute('src');
        const resolved = currentSource && !currentSource.startsWith('data:') ? currentSource : lazySource || currentSource;
        if (resolved) {
            img.setAttribute('src', toAbsolute(resolved));
        }
        img.removeAttribute('srcset');
        img.removeAttribute('sizes');
        img.removeAttribute('loading');
    }

    document.querySelectorAll('picture source').forEach((source) => source.remove());

    for (const element of document.querySelectorAll('[src]')) {
        element.setAttribute('src', toAbsolute(element.getAttribute('src')));
    }

    for (const element of document.querySelectorAll('link[href], a[href], area[href]')) {
        element.setAttribute('href', toAbsolute(element.getAttribute('href')));
    }

    for (const element of document.querySelectorAll('[style*="url("]')) {
        element.setAttribute('style', element.getAttribute('style').replace(
            /url\(\s*(['"]?)([^'")]+)\1\s*\)/gi,
            (_, quote, url) => `url(${quote}${toAbsolute(url)}${quote})`));
    }

    for (const frame of document.querySelectorAll('iframe, frame')) {
        frame.setAttribute('src', 'about:blank');
        frame.removeAttribute('srcdoc');
    }

    const removable = [
        'script',
        'noscript',
        'base',
        'object',
        'embed',
        'meta[http-equiv]',
        'link[rel~="preload"][as="script"]',
        'link[rel~="modulepreload"]',
        'link[rel~="manifest"]',
        'link[rel~="serviceworker"]'
    ];
    document.querySelectorAll(removable.join(',')).forEach((node) => node.remove());

    for (const element of document.querySelectorAll('*')) {
        for (const attribute of Array.from(element.attributes)) {
            const name = attribute.name.toLowerCase();
            const value = attribute.value.trim().toLowerCase();
            if (name.startsWith('on') || ((name === 'href' || name === 'action' || name === 'formaction') && value.startsWith('javascript:'))) {
                element.removeAttribute(attribute.name);
            }
        }
    }

    for (const input of document.querySelectorAll('input')) {
        if (input.value && input.type !== 'password') {
            input.setAttribute('value', input.value);
        }
    }

    return baseHref;
}
