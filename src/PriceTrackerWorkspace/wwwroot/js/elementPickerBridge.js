const INSPECTOR_SOURCE = 'pt-inspector';
const BRIDGE_SOURCE = 'pt-bridge';
const MAX_TEXT_LENGTH = 500;

const truncate = (value, length = MAX_TEXT_LENGTH) => String(value ?? '').slice(0, length);

export function attach(iframe, dotNetRef) {
    const handleMessage = (event) => {
        if (event.source !== iframe.contentWindow) {
            return;
        }

        const data = event.data;
        if (!data || data.source !== INSPECTOR_SOURCE) {
            return;
        }

        switch (data.type) {
            case 'ready':
                dotNetRef.invokeMethodAsync('OnInspectorReady', truncate(data.pageTitle, 300));
                break;
            case 'selected':
                dotNetRef.invokeMethodAsync('OnElementSelected', {
                    selector: truncate(data.selector, 1024),
                    rawText: truncate(data.rawText),
                    pageTitle: truncate(data.pageTitle, 300),
                    tagName: truncate(data.tagName, 32),
                    matchCount: Number(data.matchCount) || 0
                });
                break;
            case 'evaluated':
                dotNetRef.invokeMethodAsync('OnSelectorEvaluated', {
                    selector: truncate(data.selector, 1024),
                    rawText: data.rawText == null ? null : truncate(data.rawText),
                    matchCount: Number(data.matchCount) || 0,
                    error: data.error == null ? null : truncate(data.error, 200)
                });
                break;
        }
    };

    window.addEventListener('message', handleMessage);

    return {
        async load(streamRef) {
            const buffer = await streamRef.arrayBuffer();
            iframe.srcdoc = new TextDecoder('utf-8').decode(buffer);
        },

        evaluate(selector) {
            iframe.contentWindow?.postMessage({ source: BRIDGE_SOURCE, type: 'evaluate', selector }, '*');
        },

        dispose() {
            window.removeEventListener('message', handleMessage);
            iframe.removeAttribute('srcdoc');
        }
    };
}
