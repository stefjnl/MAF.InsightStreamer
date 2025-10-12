// Lightweight markdown utility with lazy loading capability
window.Markdown = (function() {
    // Check if internal parser is available
    const hasInternalParser = typeof window.Messages !== 'undefined' && typeof window.Messages.parseMarkdown !== 'undefined';
    
    // Internal parser function (if available)
    function useInternalParser(text) {
        if (hasInternalParser && typeof window.Messages.parseMarkdown === 'function') {
            return window.Messages.parseMarkdown(text);
        }
        // Fallback to basic text rendering if internal parser not available
        return text.replace(/\n/g, '<br>');
    }
    
    // Flag to track if marked.js is being loaded
    let markedLoadingPromise = null;
    
    // Function to load marked.js from CDN
    async function loadMarked() {
        if (window.marked) {
            return Promise.resolve(window.marked);
        }
        
        if (markedLoadingPromise) {
            return markedLoadingPromise;
        }
        
        markedLoadingPromise = new Promise((resolve, reject) => {
            // Check again in case it was loaded while creating the promise
            if (window.marked) {
                resolve(window.marked);
                return;
            }
            
            // Create script element
            const script = document.createElement('script');
            script.src = 'https://cdn.jsdelivr.net/npm/marked/marked.min.js';
            script.async = true;
            
            script.onload = () => {
                if (window.marked) {
                    console.info('Marked.js loaded successfully');
                    resolve(window.marked);
                } else {
                    console.warn('Marked.js loaded but window.marked is not available');
                    reject(new Error('Marked.js not available after loading'));
                }
            };
            
            script.onerror = () => {
                console.warn('Failed to load Marked.js from CDN');
                markedLoadingPromise = null; // Reset so we can try again later
                reject(new Error('Failed to load Marked.js'));
            };
            
            document.head.appendChild(script);
        });
        
        return markedLoadingPromise;
    }
    
    // Main parse function that uses internal parser first, falls back to marked if needed
    async function parse(text, options = {}) {
        const { forceMarked = false } = options;
        
        if (!forceMarked && hasInternalParser) {
            // Try internal parser first
            try {
                return useInternalParser(text);
            } catch (error) {
                console.warn('Internal parser failed, falling back to marked if available:', error);
            }
        }
        
        // Check if marked is available or try to load it
        if (window.marked || forceMarked) {
            try {
                if (!window.marked && forceMarked) {
                    await loadMarked();
                }
                
                if (window.marked) {
                    return window.marked.parse(text);
                }
            } catch (error) {
                console.warn('Marked.js parsing failed:', error);
            }
        }
        
        // Fallback to plain text if all parsers fail
        return text.replace(/\n/g, '<br>');
    }
    
    // Expose functions
    return {
        parse: parse,
        useInternalParser: useInternalParser,
        loadMarked: loadMarked
    };
})();