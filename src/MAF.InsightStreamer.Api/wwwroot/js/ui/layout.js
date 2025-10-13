// Layout management for sidebar drawer and responsive behavior
class LayoutController {
    constructor() {
        this.drawer = document.getElementById('sidebar-drawer');
        this.scrim = document.getElementById('sidebar-scrim');
        this.hamburgerBtn = document.getElementById('hamburgerBtn');
        this.closeBtn = this.drawer.querySelector('button[aria-label="Close sidebar"]');
        
        // Track focus for accessibility
        this.previousFocus = null;
        
        // Media query for responsive behavior
        this.mediaQuery = window.matchMedia('(min-width: 1024px)');
        
        this.init();
    }
    
    init() {
        // Set initial state
        this.updateDrawerState();
        
        // Add event listeners
        this.hamburgerBtn?.addEventListener('click', () => this.toggleDrawer());
        
        // Add mobile header button handlers
        const mobileNewChatBtn = document.getElementById('mobileNewChatBtn');
        const mobileThemeToggle = document.getElementById('mobileThemeToggle');
        const mobileProviderSelect = document.getElementById('mobileProviderSelect');
        const mobileModelSelect = document.getElementById('mobileModelSelect');
        
        mobileNewChatBtn?.addEventListener('click', () => {
            window.dispatchEvent(new CustomEvent('ui:new-chat'));
            this.closeDrawer();
        });
        
        mobileThemeToggle?.addEventListener('click', () => {
            const themeBtn = document.getElementById('themeToggle');
            if (themeBtn) {
                themeBtn.click(); // Trigger existing theme toggle
            }
        });
        
        // Sync mobile and desktop provider/model selections
        mobileProviderSelect?.addEventListener('change', (e) => {
            const desktopSelect = document.getElementById('providerSelect');
            if (desktopSelect && desktopSelect.value !== e.target.value) {
                desktopSelect.value = e.target.value;
                desktopSelect.dispatchEvent(new Event('change'));
            }
        });
        
        mobileModelSelect?.addEventListener('change', (e) => {
            const desktopSelect = document.getElementById('modelSelect');
            if (desktopSelect && desktopSelect.value !== e.target.value) {
                desktopSelect.value = e.target.value;
                desktopSelect.dispatchEvent(new Event('change'));
            }
        });
        this.closeBtn?.addEventListener('click', () => this.closeDrawer());
        this.scrim?.addEventListener('click', () => this.closeDrawer());
        
        // Handle escape key
        document.addEventListener('keydown', (event) => {
            if (event.key === 'Escape' && this.isDrawerOpen()) {
                this.closeDrawer();
            }
        });
        
        // Handle viewport changes
        this.mediaQuery.addEventListener('change', () => this.handleViewportChange());
        
        // Initial viewport check
        this.handleViewportChange();

        // Ensure chat UI class is set initially
        this.applyChatUiClass();

        // Observe theme changes to sync background treatment
        document.addEventListener('theme:changed', () => this.applyChatUiClass());
    }
    
    toggleDrawer() {
        if (this.isDrawerOpen()) {
            this.closeDrawer();
        } else {
            this.openDrawer();
        }
    }
    
    openDrawer() {
        // Store current focus for restoration
        this.previousFocus = document.activeElement;
        
        // Update state
        document.documentElement.setAttribute('data-drawer', 'open');
        this.drawer.setAttribute('aria-hidden', 'false');
        this.drawer.classList.remove('hidden');
        this.scrim.classList.remove('hidden');
        this.hamburgerBtn.setAttribute('aria-expanded', 'true');
        
        // Focus first focusable element in drawer
        const firstFocusable = this.drawer.querySelector('input, button, [href], [tabindex]:not([tabindex="-1"])');
        if (firstFocusable) {
            firstFocusable.focus();
        } else {
            // If no specific focusable element found, focus the drawer itself
            this.drawer.focus();
        }
        
        // Trap focus within the drawer
        this.trapFocusInDrawer();
        
        // Announce drawer opening to screen readers
        this.announceToScreenReader('Sidebar drawer opened. Press Escape to close.');
    }
    
    closeDrawer() {
        // Remove focus trap
        this.removeFocusTrap();
        
        // Update state
        document.documentElement.removeAttribute('data-drawer');
        this.drawer.setAttribute('aria-hidden', 'true');
        this.drawer.classList.add('hidden');
        this.scrim.classList.add('hidden');
        this.hamburgerBtn.setAttribute('aria-expanded', 'false');
        
        // Restore focus to previous element
        if (this.previousFocus && this.previousFocus !== this.closeBtn) {
            this.previousFocus.focus();
        } else {
            this.hamburgerBtn.focus();
        }
        
        // Announce drawer closing to screen readers
        this.announceToScreenReader('Sidebar drawer closed.');
    }
    
    isDrawerOpen() {
        return document.documentElement.getAttribute('data-drawer') === 'open';
    }
    
    updateDrawerState() {
        const isOpen = this.isDrawerOpen();
        this.drawer.setAttribute('aria-hidden', (!isOpen).toString());
        this.hamburgerBtn.setAttribute('aria-expanded', isOpen.toString());
    }
    
    handleViewportChange() {
        // If viewport becomes >= 1024px, ensure drawer is closed
        if (this.mediaQuery.matches) {
            this.closeDrawer();
        }
    }

    applyChatUiClass() {
        const body = document.body;
        if (!body.classList.contains('chat-ui')) {
            body.classList.add('chat-ui');
        }
    }
    
    // Trap focus within the drawer
    trapFocusInDrawer() {
        // Remove any existing focus trap listeners
        this.removeFocusTrap();
        
        // Add keydown listener to trap focus
        this.focusTrapHandler = (event) => {
            if (event.key === 'Tab') {
                const focusableElements = this.drawer.querySelectorAll(
                    'button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'
                );
                
                if (focusableElements.length === 0) return;
                
                const firstElement = focusableElements[0];
                const lastElement = focusableElements[focusableElements.length - 1];
                
                if (event.shiftKey) {
                    // Shift + Tab: if first element is focused, go to last
                    if (document.activeElement === firstElement) {
                        lastElement.focus();
                        event.preventDefault();
                    }
                } else {
                    // Tab: if last element is focused, go to first
                    if (document.activeElement === lastElement) {
                        firstElement.focus();
                        event.preventDefault();
                    }
                }
            }
        };
        
        document.addEventListener('keydown', this.focusTrapHandler);
    }
    
    // Remove focus trap
    removeFocusTrap() {
        if (this.focusTrapHandler) {
            document.removeEventListener('keydown', this.focusTrapHandler);
            this.focusTrapHandler = null;
        }
    }
    
    // Function to announce messages to screen readers
    announceToScreenReader(message) {
        const announcementElement = document.getElementById('screen-reader-announcements');
        if (announcementElement) {
            // Clear previous announcement and add new one
            announcementElement.textContent = '';
            // Add a small delay to ensure screen readers pick up the change
            setTimeout(() => {
                announcementElement.textContent = message;
            }, 100);
        }
    }
}

// Initialize layout controller when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    window.layoutController = new LayoutController();
});