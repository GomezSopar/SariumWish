() => {
    const candidates = [];
    const seen = new Set();

    const clean = (value) => String(value ?? '').replace(/\s+/g, ' ').trim();

    const isVisible = (element) => {
        const rect = element.getBoundingClientRect();
        if (rect.width === 0 || rect.height === 0) {
            return false;
        }
        const style = getComputedStyle(element);
        return style.visibility !== 'hidden' && style.display !== 'none' && Number(style.opacity) > 0;
    };

    const readElement = (element) => {
        if (element.tagName === 'META') {
            return clean(element.getAttribute('content'));
        }
        const content = element.getAttribute('content');
        const text = clean(element.innerText || element.textContent);
        return text || clean(content);
    };

    const push = (kind, selector, text, currency) => {
        const value = clean(text);
        if (!value || !/\d/.test(value)) {
            return;
        }
        const key = `${selector}|${value}`;
        if (seen.has(key)) {
            return;
        }
        seen.add(key);
        candidates.push({ kind, selector, text: value.slice(0, 160), currency: currency ? clean(currency) : null });
    };

    const findOffers = (node, depth = 0) => {
        if (!node || typeof node !== 'object' || depth > 8) {
            return;
        }
        if (Array.isArray(node)) {
            node.forEach((child) => findOffers(child, depth + 1));
            return;
        }
        const offerList = node.offers ? (Array.isArray(node.offers) ? node.offers : [node.offers]) : [];
        for (const offer of offerList) {
            const price = offer?.price ?? offer?.lowPrice ?? offer?.priceSpecification?.price;
            if (price !== undefined && price !== null) {
                push('structured', null, String(price), offer.priceCurrency ?? offer?.priceSpecification?.priceCurrency);
            }
        }
        for (const value of Object.values(node)) {
            if (value && typeof value === 'object') {
                findOffers(value, depth + 1);
            }
        }
    };

    for (const script of document.querySelectorAll('script[type="application/ld+json"]')) {
        try {
            findOffers(JSON.parse(script.textContent));
        } catch {
        }
    }

    const metaCurrency = document.querySelector('meta[property="product:price:currency"], meta[property="og:price:currency"]')?.getAttribute('content');
    for (const selector of ['meta[property="product:price:amount"]', 'meta[property="og:price:amount"]']) {
        const meta = document.querySelector(selector);
        if (meta) {
            push('structured', selector, meta.getAttribute('content'), metaCurrency);
        }
    }

    const itemPropCurrency = document.querySelector('[itemprop="priceCurrency"]');
    const itemPropCurrencyValue = itemPropCurrency ? (itemPropCurrency.getAttribute('content') || itemPropCurrency.textContent) : null;
    const itemPropPrice = document.querySelector('[itemprop="price"]');
    if (itemPropPrice) {
        push('structured', '[itemprop="price"]', readElement(itemPropPrice), itemPropCurrencyValue);
    }

    const specificSelectors = [
        '#corePrice_feature_div .a-price .a-offscreen',
        '#corePriceDisplay_desktop_feature_div .a-price .a-offscreen',
        '#priceblock_ourprice',
        '#priceblock_dealprice',
        '.a-price .a-offscreen',
        '.a-price-whole',
        '[data-testid="price"]',
        '[data-test="product-price"]',
        '[data-automation-id="product-price"]',
        '[data-qa="price"]',
        '.price_color'
    ];

    const genericSelectors = ['.product-price', '.price-current', '.current-price', '.sale-price', '.price'];

    const probe = (selector, kind) => {
        const element = document.querySelector(selector);
        if (!element) {
            return;
        }
        if (selector === '.a-price-whole') {
            const fraction = element.parentElement?.querySelector('.a-price-fraction')?.textContent ?? '';
            const symbol = element.parentElement?.querySelector('.a-price-symbol')?.textContent ?? '';
            const whole = clean(element.textContent).replace(/[.,]$/, '');
            push(kind, selector, `${symbol}${whole}${fraction ? '.' + clean(fraction) : ''}`, null);
            return;
        }
        push(kind, selector, readElement(element), null);
    };

    specificSelectors.forEach((selector) => probe(selector, 'specific'));
    genericSelectors.forEach((selector) => probe(selector, 'generic'));

    const broad = [];
    for (const element of document.querySelectorAll('[class*="price" i], [id*="price" i], [data-price]')) {
        if (broad.length >= 60) {
            break;
        }
        if (!isVisible(element)) {
            continue;
        }
        const text = clean(element.innerText);
        if (!text || text.length > 40 || !/\d/.test(text)) {
            continue;
        }
        const rect = element.getBoundingClientRect();
        if (rect.top + window.scrollY > 2200) {
            continue;
        }
        const fontSize = parseFloat(getComputedStyle(element).fontSize) || 0;
        broad.push({ text, fontSize, top: rect.top });
    }

    broad
        .sort((a, b) => b.fontSize - a.fontSize || a.top - b.top)
        .slice(0, 8)
        .forEach((entry) => push('broad', '[class*="price"]', entry.text, null));

    return JSON.stringify({ title: document.title, candidates });
}
