window.Errors = (function() {
    // Inline error bubble
    function showInlineError(messageId, errorText, onRetry) {
        // Find the message element by data-id
        const messageElement = document.querySelector(`[data-id="${messageId}"]`);
        if (!messageElement) {
            console.warn(`Message element with data-id="${messageId}" not found`);
            return;
        }

        // Remove any existing error bubbles for this message
        const existingError = messageElement.querySelector('.error-bubble');
        if (existingError) {
            existingError.remove();
        }

        // Create error bubble element
        const errorBubble = document.createElement('div');
        errorBubble.className = "error-bubble mt-2 rounded-md border border-rose-200 dark:border-rose-900 bg-rose-50 dark:bg-neutral-900 text-rose-800 dark:text-rose-300 p-3 text-sm flex items-start justify-between gap-3";
        errorBubble.innerHTML = `
            <span class="flex-1">${errorText}</span>
            <button class="retry-btn text-xs px-2 py-1 rounded border border-rose-300 dark:border-neutral-700 hover:bg-rose-100 dark:hover:bg-neutral-800 transition">
                Retry
            </button>
        `;

        // Add to the message container after the message content
        messageElement.appendChild(errorBubble);

        // Add event listener for retry button
        const retryBtn = errorBubble.querySelector('.retry-btn');
        retryBtn.addEventListener('click', function() {
            if (typeof onRetry === 'function') {
                onRetry();
            }
            // Remove the error bubble after retry
            errorBubble.remove();
        });
    }

    // Provider banner
    function showProviderBanner(state, text, onRetry) {
        const banner = document.getElementById('providerBanner');
        const bannerText = document.getElementById('providerBannerText');
        const retryBtn = document.getElementById('providerRetryBtn');

        // Update state-dependent classes
        const baseClasses = 'mx-auto max-w-[1024px] px-4 py-2 mt-2 rounded-md border text-sm flex items-start justify-between gap-3';
        
        // Remove all state classes
        banner.classList.remove(
            'bg-emerald-50', 'border-emerald-200', 'text-emerald-800',
            'bg-amber-50', 'border-amber-200', 'text-amber-800',
            'bg-rose-50', 'border-rose-200', 'text-rose-800',
            'bg-slate-50', 'border-slate-200', 'text-slate-800',
            'dark:bg-neutral-900', 'dark:border-neutral-800', 'dark:text-amber-300',
            'dark:text-emerald-300', 'dark:text-rose-300', 'dark:text-slate-300'
        );

        // Add classes based on state
        switch (state) {
            case 'connected':
                banner.classList.add('bg-emerald-50', 'border-emerald-200', 'text-emerald-800', 'dark:text-emerald-300');
                // Update dot color
                const dot = banner.querySelector('.w-2.h-2.rounded-full');
                dot.className = dot.className.replace(/bg-\w+-500/, 'bg-emerald-500');
                break;
            case 'rate_limited':
                banner.classList.add('bg-amber-50', 'border-amber-200', 'text-amber-800', 'dark:bg-neutral-900', 'dark:border-neutral-800', 'dark:text-amber-300');
                // Update dot color
                const rateLimitedDot = banner.querySelector('.w-2.h-2.rounded-full');
                rateLimitedDot.className = rateLimitedDot.className.replace(/bg-\w+-500/, 'bg-amber-500');
                break;
            case 'unavailable':
                banner.classList.add('bg-rose-50', 'border-rose-200', 'text-rose-800', 'dark:bg-neutral-900', 'dark:border-neutral-800', 'dark:text-rose-300');
                // Update dot color
                const unavailableDot = banner.querySelector('.w-2.h-2.rounded-full');
                unavailableDot.className = unavailableDot.className.replace(/bg-\w+-500/, 'bg-rose-500');
                break;
            case 'info':
            default:
                banner.classList.add('bg-slate-50', 'border-slate-200', 'text-slate-800', 'dark:bg-neutral-900', 'dark:border-neutral-800', 'dark:text-slate-300');
                // Update dot color
                const infoDot = banner.querySelector('.w-2.h-2.rounded-full');
                infoDot.className = infoDot.className.replace(/bg-\w+-500/, 'bg-slate-500');
                break;
        }

        // Update text
        bannerText.textContent = text;

        // Update retry button visibility and handler
        if (typeof onRetry === 'function') {
            retryBtn.style.display = 'block';
            retryBtn.onclick = onRetry;
        } else {
            retryBtn.style.display = 'none';
        }

        // Show banner
        banner.classList.remove('hidden');
        
        // Set aria-live="polite" on the banner text for status announcements
        bannerText.setAttribute('aria-live', 'polite');
    }

    function hideProviderBanner() {
        const banner = document.getElementById('providerBanner');
        banner.classList.add('hidden');
    }

    // Diagnostics modal
    let backoffInterval = null;
    let previouslyFocusedElement = null;

    function showDiagnostics(payload) {
        const overlay = document.getElementById('diagnosticsOverlay');
        const modal = document.getElementById('diagnosticsModal');
        const messageEl = document.getElementById('diagMessage');
        const detailsEl = document.getElementById('diagDetails');
        const backoffEl = document.getElementById('diagBackoff');
        const retryBtn = document.getElementById('diagRetry');

        // Store the currently focused element to restore focus later
        previouslyFocusedElement = document.activeElement;

        // Set message and details
        messageEl.textContent = payload.error || 'An error occurred during model discovery';
        detailsEl.textContent = payload.details ? JSON.stringify(payload.details, null, 2) : '';

        // Show modal
        overlay.classList.remove('hidden');
        modal.classList.remove('hidden');

        // Focus on the modal for accessibility - focus on the close button
        const firstFocusable = modal.querySelector('button');
        if (firstFocusable) {
            firstFocusable.focus();
        }

        // Set up event listeners for closing modal
        const closeBtn = document.getElementById('diagClose');
        const bannerCloseBtn = document.getElementById('providerBannerClose');
        const handleOverlayClick = (e) => {
            if (e.target === overlay) {
                hideDiagnostics();
            }
        };
        const handleEscape = (e) => {
            if (e.key === 'Escape') {
                hideDiagnostics();
            }
        };

        closeBtn.onclick = hideDiagnostics;
        bannerCloseBtn.onclick = hideDiagnostics;
        overlay.addEventListener('click', handleOverlayClick);
        document.addEventListener('keydown', handleEscape);

        // Set up focus trap for accessibility
        const focusableElements = modal.querySelectorAll('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])');
        const firstFocusableElement = focusableElements[0];
        const lastFocusableElement = focusableElements[focusableElements.length - 1];

        const focusTrap = (e) => {
            if (e.key === 'Tab') {
                if (e.shiftKey) {
                    if (document.activeElement === firstFocusableElement) {
                        e.preventDefault();
                        lastFocusableElement.focus();
                    }
                } else {
                    if (document.activeElement === lastFocusableElement) {
                        e.preventDefault();
                        firstFocusableElement.focus();
                    }
                }
            }
        };

        document.addEventListener('keydown', focusTrap);

        // Set up retry handler with backoff
        let currentAttempt = payload.attempt || 1;
        let backoffSeconds = Math.min(Math.pow(2, currentAttempt), 16); // exponential backoff up to 16s

        const startBackoff = () => {
            if (backoffInterval) {
                clearInterval(backoffInterval);
            }

            backoffInterval = setInterval(() => {
                backoffSeconds--;
                backoffEl.textContent = `Retry available in ${backoffSeconds}s`;
                if (backoffSeconds <= 0) {
                    clearInterval(backoffInterval);
                    backoffInterval = null;
                    backoffEl.textContent = '';
                    retryBtn.disabled = false;
                }
            }, 1000);

            retryBtn.disabled = true;
            backoffEl.textContent = `Retry available in ${backoffSeconds}s`;
        };

        retryBtn.onclick = () => {
            // Dispatch event for model discovery retry
            window.dispatchEvent(new CustomEvent('ui:model-discovery-retry', {
                detail: { provider: payload.provider }
            }));

            // Start backoff
            startBackoff();

            // Emit another event if provided
            if (typeof payload.onRetry === 'function') {
                payload.onRetry();
            } else {
                window.dispatchEvent(new CustomEvent('ui:model-discovery-retry-now', {
                    detail: { provider: payload.provider }
                }));
            }
        };

        // Start backoff immediately
        startBackoff();
    }

    function hideDiagnostics() {
        const overlay = document.getElementById('diagnosticsOverlay');
        const modal = document.getElementById('diagnosticsModal');

        // Clear backoff interval if exists
        if (backoffInterval) {
            clearInterval(backoffInterval);
            backoffInterval = null;
        }

        // Remove event listeners
        overlay.removeEventListener('click', handleOverlayClick);
        document.removeEventListener('keydown', handleEscape);
        document.removeEventListener('keydown', focusTrap);

        // Hide modal
        overlay.classList.add('hidden');
        modal.classList.add('hidden');
        
        // Restore focus to the element that was focused before the modal opened
        if (previouslyFocusedElement && previouslyFocusedElement.focus) {
            previouslyFocusedElement.focus();
        }
    }

    // Event listeners
    function handleProviderError(e) {
        showProviderBanner('unavailable', e.detail.text || 'Provider error detected');
    }

    function handleProviderRateLimited(e) {
        showProviderBanner('rate_limited', e.detail.text || 'Provider is rate limited');
    }

    function handleProviderRecovered(e) {
        hideProviderBanner();
    }

    function handleModelDiscoveryFailed(e) {
        showDiagnostics(e.detail);
    }

    // Set up event listeners when DOM is loaded
    document.addEventListener('DOMContentLoaded', function() {
        window.addEventListener('ui:provider-error', handleProviderError);
        window.addEventListener('ui:provider-rate-limited', handleProviderRateLimited);
        window.addEventListener('ui:provider-recovered', handleProviderRecovered);
        window.addEventListener('ui:model-discovery-failed', handleModelDiscoveryFailed);
    });

    // Helper function to handle overlay click (defined here for use in hideDiagnostics)
    function handleOverlayClick(e) {
        const overlay = document.getElementById('diagnosticsOverlay');
        if (e.target === overlay) {
            hideDiagnostics();
        }
    }

    // Helper function to handle Escape key (defined here for use in hideDiagnostics)
    function handleEscape(e) {
        if (e.key === 'Escape') {
            hideDiagnostics();
        }
    }

    return {
        showInlineError,
        showProviderBanner,
        hideProviderBanner,
        showDiagnostics,
        hideDiagnostics
    };
})();