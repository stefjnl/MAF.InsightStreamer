/**
 * Theme controller for managing light/dark/system theme preferences
 */
window.Theme = (function() {
  const THEME_KEY = 'is:theme';
  
  // Available theme preferences
  const THEMES = {
    SYSTEM: 'system',
    LIGHT: 'light',
    DARK: 'dark'
  };
  
  /**
   * Apply a theme preference
   * @param {string} preference - One of 'system', 'light', or 'dark'
   */
  function apply(preference) {
    const html = document.documentElement;
    
    // Remove any existing theme classes
    html.classList.remove('dark');
    
    switch(preference) {
      case THEMES.SYSTEM:
        // Remove data-theme attribute to let OS preference take over
        html.removeAttribute('data-theme');
        // Set meta color-scheme to support both light and dark
        document.querySelector('meta[name="color-scheme"]').setAttribute('content', 'light dark');
        break;
        
      case THEMES.LIGHT:
        // Apply light theme via data attribute
        html.setAttribute('data-theme', 'light');
        // Set meta color-scheme to light only
        document.querySelector('meta[name="color-scheme"]').setAttribute('content', 'light');
        break;
        
      case THEMES.DARK:
        // Apply dark theme via data attribute
        html.setAttribute('data-theme', 'dark');
        // Set meta color-scheme to dark only
        document.querySelector('meta[name="color-scheme"]').setAttribute('content', 'dark');
        // Add dark class for future Tailwind class-mode compatibility
        html.classList.add('dark');
        break;
        
      default:
        console.warn(`Unknown theme preference: ${preference}`);
        apply(THEMES.SYSTEM);
        return;
    }
  }
  
  /**
   * Initialize theme management
   */
  function init() {
    // Load saved preference from localStorage (default to 'system')
    const savedPreference = localStorage.getItem(THEME_KEY) || THEMES.SYSTEM;
    
    // Apply the loaded theme
    apply(savedPreference);
    
    // Set up theme toggle button
    const themeToggle = document.getElementById('themeToggle');
    if (themeToggle) {
      // Update button with current theme
      updateButtonLabel(themeToggle, savedPreference);
      
      // Wire up click handler to cycle through themes
      themeToggle.addEventListener('click', function() {
        // Get current theme from button text or saved preference
        const currentTheme = getCurrentTheme();
        let nextTheme;
        
        // Cycle: system -> dark -> light -> system
        switch(currentTheme) {
          case THEMES.SYSTEM:
            nextTheme = THEMES.DARK;
            break;
          case THEMES.DARK:
            nextTheme = THEMES.LIGHT;
            break;
          case THEMES.LIGHT:
          default:
            nextTheme = THEMES.SYSTEM;
            break;
        }
        
        // Apply the new theme
        apply(nextTheme);
        
        // Save preference to localStorage
        localStorage.setItem(THEME_KEY, nextTheme);
        
        // Update button label and aria attributes
        updateButtonLabel(themeToggle, nextTheme);
      });
    }
  }
  
  /**
   * Get the current theme preference
   * @returns {string} - Current theme preference
   */
  function getCurrentTheme() {
    const saved = localStorage.getItem(THEME_KEY);
    return saved || THEMES.SYSTEM;
  }
  
  /**
   * Update the theme toggle button label and attributes
   * @param {HTMLElement} button - The theme toggle button
   * @param {string} theme - Current theme
   */
  function updateButtonLabel(button, theme) {
    // Set button text based on current theme
    switch(theme) {
      case THEMES.SYSTEM:
        button.textContent = 'System';
        button.setAttribute('aria-label', 'Change theme (currently system, click to switch to dark)');
        button.setAttribute('title', 'Click to switch to Dark');
        button.setAttribute('aria-pressed', 'false');
        break;
        
      case THEMES.LIGHT:
        button.textContent = 'Light';
        button.setAttribute('aria-label', 'Change theme (currently light, click to switch to system)');
        button.setAttribute('title', 'Click to switch to System');
        button.setAttribute('aria-pressed', 'true');
        break;
        
      case THEMES.DARK:
        button.textContent = 'Dark';
        button.setAttribute('aria-label', 'Change theme (currently dark, click to switch to system)');
        button.setAttribute('title', 'Click to switch to System');
        button.setAttribute('aria-pressed', 'true');
        break;
    }
  }
  
  // Expose public API
  return {
    apply: apply,
    init: init
  };
})();

// Initialize theme management when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
  window.Theme.init();
});