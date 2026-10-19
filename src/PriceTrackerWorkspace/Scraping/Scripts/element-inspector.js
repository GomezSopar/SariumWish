(() => {
    'use strict';

    const SOURCE = 'pt-inspector';
    const BRIDGE_SOURCE = 'pt-bridge';
    const HOVER_CLASS = '__pt-hover';
    const SELECTED_CLASS = '__pt-selected';
    const MATCH_CLASS = '__pt-match';
    const INTERNAL_PREFIX = '__pt-';
    const PREFERRED_ATTRIBUTES = [
        'data-testid', 'data-test-id', 'data-test', 'data-qa', 'data-cy',
        'data-automation-id', 'data-automation', 'data-component', 'data-price-type',
        'itemprop', 'itemtype', 'name', 'aria-label', 'role'
    ];
    const IGNORED_DATA_ATTRIBUTES = /^data-(reactid|reactroot|v-|react-|styled|emotion|gtm|ga|track|analytics|aos|lazy|src|srcset|original|uid|id$|key$|index$|ved$|hveid$)/i;

    const tooltip = document.createElement('div');
    tooltip.id = '__pt-tooltip';
    document.documentElement.appendChild(tooltip);

    let hovered = null;
    let selected = null;

    const post = (type, payload = {}) => window.parent.postMessage({ source: SOURCE, type, ...payload }, '*');

    const isInternal = (element) => element === tooltip || (element.id && element.id.startsWith(INTERNAL_PREFIX));

    const collapse = (value) => String(value ?? '').replace(/\s+/g, ' ').trim();

    const readText = (element) => {
        if (element.tagName === 'META') {
            return collapse(element.getAttribute('content')).slice(0, 500);
        }
        const text = collapse(element.innerText || element.textContent);
        return (text || collapse(element.getAttribute('content'))).slice(0, 500);
    };

    const looksGenerated = (token) =>
        token.length > 48
        || token.startsWith(INTERNAL_PREFIX)
        || /\d{4,}/.test(token)
        || /^(css|sc|jsx|emotion|styled|svelte|astro|jss|makeStyles|tw)-/i.test(token)
        || /__[A-Za-z0-9_-]{5,}$/.test(token)
        || /^_[A-Za-z0-9]{5,}$/.test(token)
        || /^[a-zA-Z]{1,3}-?[A-Za-z0-9]*\d[A-Za-z0-9]*[A-Z][A-Za-z0-9]*$/.test(token)
        || /^[a-z0-9]{6,}$/.test(token) && /\d/.test(token) && /[a-z]/.test(token);

    const quote = (value) => `"${String(value).replace(/\\/g, '\\\\').replace(/"/g, '\\"')}"`;

    const matchesOnly = (selector, target, root = document) => {
        try {
            const matches = root.querySelectorAll(selector);
            return matches.length === 1 && matches[0] === target;
        } catch {
            return false;
        }
    };

    const stableClasses = (element) =>
        Array.from(element.classList).filter((token) => !looksGenerated(token)).slice(0, 4);

    const localCandidates = (element) => {
        const tag = element.tagName.toLowerCase();
        const candidates = [];

        if (element.id && !looksGenerated(element.id)) {
            candidates.push(`#${CSS.escape(element.id)}`);
        }

        for (const attribute of PREFERRED_ATTRIBUTES) {
            const value = element.getAttribute(attribute);
            if (value && value.length <= 80 && !looksGenerated(value.replace(/\s+/g, ''))) {
                candidates.push(`${tag}[${attribute}=${quote(value)}]`);
            }
        }

        for (const attribute of Array.from(element.attributes)) {
            const name = attribute.name;
            if (!name.startsWith('data-') || IGNORED_DATA_ATTRIBUTES.test(name) || PREFERRED_ATTRIBUTES.includes(name)) {
                continue;
            }
            const value = attribute.value;
            if (value === '') {
                candidates.push(`${tag}[${name}]`);
            } else if (value.length <= 60 && !looksGenerated(value.replace(/\s+/g, ''))) {
                candidates.push(`${tag}[${name}=${quote(value)}]`);
            }
        }

        const classes = stableClasses(element).map((token) => `.${CSS.escape(token)}`);
        classes.forEach((cls) => candidates.push(`${tag}${cls}`));
        for (let i = 0; i < classes.length; i++) {
            for (let j = i + 1; j < classes.length; j++) {
                candidates.push(`${tag}${classes[i]}${classes[j]}`);
            }
        }

        return candidates;
    };

    const segmentFor = (element) => {
        const tag = element.tagName.toLowerCase();
        const parent = element.parentElement;
        if (!parent || tag === 'body' || tag === 'html') {
            return tag;
        }

        for (const candidate of localCandidates(element)) {
            if (candidate.startsWith('#') && matchesOnly(candidate, element)) {
                return candidate;
            }
            const siblingsMatching = Array.from(parent.children).filter((child) => {
                try {
                    return child.matches(candidate);
                } catch {
                    return false;
                }
            });
            if (siblingsMatching.length === 1) {
                return candidate;
            }
        }

        const sameTag = Array.from(parent.children).filter((child) => child.tagName === element.tagName);
        if (sameTag.length === 1) {
            return tag;
        }
        return `${tag}:nth-of-type(${sameTag.indexOf(element) + 1})`;
    };

    const buildSelector = (target) => {
        const own = localCandidates(target);
        for (const candidate of own) {
            if (matchesOnly(candidate, target)) {
                return candidate;
            }
        }

        let ancestor = target.parentElement;
        for (let depth = 0; ancestor && ancestor !== document.documentElement && depth < 8; depth++) {
            for (const anchor of localCandidates(ancestor)) {
                if (!matchesOnly(anchor, ancestor)) {
                    continue;
                }
                for (const candidate of [...own, segmentFor(target)]) {
                    const combined = `${anchor} ${candidate}`;
                    if (matchesOnly(combined, target)) {
                        return combined;
                    }
                }
            }
            ancestor = ancestor.parentElement;
        }

        const parts = [];
        let node = target;
        while (node && node !== document.documentElement) {
            const segment = segmentFor(node);
            parts.unshift(segment);
            const selector = parts.join(' > ');
            if (matchesOnly(selector, target)) {
                return selector;
            }
            if (segment.startsWith('#')) {
                break;
            }
            node = node.parentElement;
        }
        return parts.join(' > ');
    };

    const describe = (element) => {
        const tag = element.tagName.toLowerCase();
        const id = element.id && !isInternal(element) ? `#${element.id}` : '';
        const classes = Array.from(element.classList)
            .filter((token) => !token.startsWith(INTERNAL_PREFIX))
            .slice(0, 3)
            .map((token) => `.${token}`)
            .join('');
        return { label: `${tag}${id}${classes}`, text: readText(element).slice(0, 90) };
    };

    const renderTooltip = (element) => {
        const { label, text } = describe(element);
        tooltip.replaceChildren();
        const labelNode = document.createElement('span');
        labelNode.className = '__pt-tooltip-tag';
        labelNode.textContent = label;
        tooltip.appendChild(labelNode);
        if (text) {
            const textNode = document.createElement('span');
            textNode.className = '__pt-tooltip-text';
            textNode.textContent = text;
            tooltip.appendChild(textNode);
        }
        tooltip.style.display = 'block';
    };

    const positionTooltip = (x, y) => {
        const offset = 14;
        const { width, height } = tooltip.getBoundingClientRect();
        const left = x + offset + width > window.innerWidth ? Math.max(4, x - width - offset) : x + offset;
        const top = y + offset + height > window.innerHeight ? Math.max(4, y - height - offset) : y + offset;
        tooltip.style.left = `${left}px`;
        tooltip.style.top = `${top}px`;
    };

    const clearClass = (className) => {
        document.querySelectorAll(`.${className}`).forEach((element) => element.classList.remove(className));
    };

    const select = (element) => {
        if (!(element instanceof Element) || isInternal(element) || element === document.documentElement) {
            return;
        }
        clearClass(SELECTED_CLASS);
        clearClass(MATCH_CLASS);
        selected = element;
        element.classList.add(SELECTED_CLASS);

        const selector = buildSelector(element);
        let matchCount = 0;
        try {
            matchCount = document.querySelectorAll(selector).length;
        } catch {
        }

        post('selected', {
            selector,
            rawText: readText(element),
            pageTitle: document.title,
            tagName: element.tagName.toLowerCase(),
            matchCount
        });
    };

    const evaluate = (selector) => {
        clearClass(MATCH_CLASS);
        try {
            const matches = document.querySelectorAll(selector);
            matches.forEach((element) => element.classList.add(MATCH_CLASS));
            matches[0]?.scrollIntoView({ block: 'center', behavior: 'smooth' });
            post('evaluated', {
                selector,
                matchCount: matches.length,
                rawText: matches[0] ? readText(matches[0]) : null,
                error: null
            });
        } catch {
            post('evaluated', { selector, matchCount: 0, rawText: null, error: 'The selector is not valid CSS.' });
        }
    };

    document.addEventListener('mouseover', (event) => {
        const element = event.target;
        if (!(element instanceof Element) || isInternal(element)) {
            return;
        }
        hovered?.classList.remove(HOVER_CLASS);
        hovered = element;
        element.classList.add(HOVER_CLASS);
        renderTooltip(element);
    }, true);

    document.addEventListener('mousemove', (event) => positionTooltip(event.clientX, event.clientY), true);

    document.documentElement.addEventListener('mouseleave', () => {
        hovered?.classList.remove(HOVER_CLASS);
        hovered = null;
        tooltip.style.display = 'none';
    });

    const blockedEvents = ['click', 'dblclick', 'auxclick', 'mousedown', 'mouseup', 'pointerdown', 'pointerup', 'touchstart', 'submit', 'contextmenu'];
    for (const type of blockedEvents) {
        document.addEventListener(type, (event) => {
            event.preventDefault();
            event.stopPropagation();
            event.stopImmediatePropagation();
            if (type === 'click') {
                select(event.target);
            }
        }, { capture: true, passive: false });
    }

    document.addEventListener('keydown', (event) => {
        if (!selected) {
            return;
        }
        if (event.key === 'ArrowUp' && selected.parentElement && selected.parentElement !== document.body) {
            event.preventDefault();
            select(selected.parentElement);
        } else if (event.key === 'ArrowDown' && selected.firstElementChild) {
            event.preventDefault();
            select(selected.firstElementChild);
        }
    }, true);

    window.addEventListener('message', (event) => {
        if (event.source !== window.parent || event.data?.source !== BRIDGE_SOURCE) {
            return;
        }
        if (event.data.type === 'evaluate' && typeof event.data.selector === 'string') {
            evaluate(event.data.selector);
        }
    });

    post('ready', { pageTitle: document.title });
})();
