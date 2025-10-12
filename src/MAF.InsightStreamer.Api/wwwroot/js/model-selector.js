// wwwroot/js/model-selector.js
class ModelSelector {
    constructor() {
        this.currentProvider = null;
        this.currentModel = null;
        this.providerCache = null;
        this.modelsCache = {};
        this.cacheExpiryTime = 5 * 60 * 1000; // 5 minutes in milliseconds
        this.init();
    }

    async init() {
        // Load the last selected provider from localStorage if available
        const savedProvider = localStorage.getItem('is:provider');
        if (savedProvider) {
            this.currentProvider = savedProvider;
        }
        
        // Prefetch providers and models on load
        this.prefetchData();
        
        await this.loadProviders();
        this.setupEventListeners();
    }

    // Prefetch providers and models on load
    async prefetchData() {
        // Prefetch providers
        try {
            const cachedProviders = this.getCachedProviders();
            if (cachedProviders) {
                // Use cached data immediately
                this.renderProviderDropdown(cachedProviders.providers);
                
                // Update the current provider selection
                const savedProvider = localStorage.getItem('is:provider');
                if (savedProvider) {
                    const providerSelect = document.getElementById('providerSelect');
                    if (providerSelect) {
                        providerSelect.value = savedProvider;
                    }
                }
                
                // Then refresh in background to keep cache fresh
                this.refreshProvidersCache();
            } else {
                this.refreshProvidersCache();
            }
        } catch (error) {
            console.warn('Failed to prefetch providers:', error);
            // Fall back to normal loading
            await this.loadProviders();
        }

        // If there's a saved provider, prefetch its models too
        const savedProvider = localStorage.getItem('is:provider');
        if (savedProvider) {
            try {
                const cachedModels = this.getCachedModels(savedProvider);
                if (cachedModels) {
                    // Use cached models immediately
                    this.renderModelDropdown(cachedModels);
                    
                    // Refresh in background to keep cache fresh
                    this.refreshModelsCache(savedProvider);
                } else {
                    this.refreshModelsCache(savedProvider);
                }
            } catch (error) {
                console.warn('Failed to prefetch models:', error);
            }
        }
    }

    // Get cached providers from memory or localStorage
    getCachedProviders() {
        try {
            // Check memory cache first
            if (this.providerCache) {
                const now = Date.now();
                if (now - this.providerCache.timestamp < this.cacheExpiryTime) {
                    return this.providerCache.data;
                }
            }

            // Check localStorage cache
            const stored = localStorage.getItem('is:providersCache');
            if (stored) {
                const parsed = JSON.parse(stored);
                const now = Date.now();
                if (now - parsed.timestamp < this.cacheExpiryTime) {
                    // Update memory cache
                    this.providerCache = parsed;
                    return parsed.data;
                } else {
                    // Cache expired, remove from localStorage
                    localStorage.removeItem('is:providersCache');
                }
            }
        } catch (error) {
            console.warn('Error reading providers cache:', error);
        }
        return null;
    }

    // Refresh providers cache from network
    async refreshProvidersCache() {
        try {
            const response = await fetch('/api/model/providers');
            if (!response.ok) {
                throw new Error(`Failed to load providers: ${response.status} ${response.statusText}`);
            }
            const data = await response.json();
            
            // Update memory cache
            this.providerCache = {
                data: data,
                timestamp: Date.now()
            };
            
            // Update localStorage cache
            localStorage.setItem('is:providersCache', JSON.stringify({
                data: data,
                timestamp: Date.now()
            }));
            
            // Only render if we haven't already rendered from cache
            const providerSelect = document.getElementById('providerSelect');
            if (providerSelect && providerSelect.options.length === 0) {
                this.renderProviderDropdown(data.providers);
                
                // Update the current provider selection
                const savedProvider = localStorage.getItem('is:provider');
                if (savedProvider) {
                    providerSelect.value = savedProvider;
                }
            }
            
            return data;
        } catch (error) {
            console.warn('Failed to refresh providers cache:', error);
            return null;
        }
    }

    // Get cached models for a provider from memory or localStorage
    getCachedModels(provider) {
        try {
            // Check memory cache first
            if (this.modelsCache[provider]) {
                const now = Date.now();
                if (now - this.modelsCache[provider].timestamp < this.cacheExpiryTime) {
                    return this.modelsCache[provider].data;
                }
            }

            // Check localStorage cache
            const cacheKey = `is:modelsCache:${provider}`;
            const stored = localStorage.getItem(cacheKey);
            if (stored) {
                const parsed = JSON.parse(stored);
                const now = Date.now();
                if (now - parsed.timestamp < this.cacheExpiryTime) {
                    // Update memory cache
                    this.modelsCache[provider] = parsed;
                    return parsed.data;
                } else {
                    // Cache expired, remove from localStorage
                    localStorage.removeItem(cacheKey);
                }
            }
        } catch (error) {
            console.warn('Error reading models cache:', error);
        }
        return null;
    }

    // Refresh models cache for a provider from network
    async refreshModelsCache(provider) {
        try {
            const response = await fetch(`/api/model/discover/${provider}`);
            if (!response.ok) {
                // Don't throw error here as it might be expected (like OpenRouter)
                if (response.status === 400) {
                    const errorText = await response.text();
                    if (errorText.includes('OpenRouter models cannot be discovered locally')) {
                        // For OpenRouter, we can't cache models but that's expected
                        return [];
                    }
                }
                return [];
            }
            const models = await response.json();
            
            // Update memory cache
            this.modelsCache[provider] = {
                data: models,
                timestamp: Date.now()
            };
            
            // Update localStorage cache
            const cacheKey = `is:modelsCache:${provider}`;
            localStorage.setItem(cacheKey, JSON.stringify({
                data: models,
                timestamp: Date.now()
            }));
            
            return models;
        } catch (error) {
            console.warn('Failed to refresh models cache:', error);
            return null;
        }
    }

    async loadProviders() {
        try {
            const cachedProviders = this.getCachedProviders();
            if (cachedProviders) {
                this.renderProviderDropdown(cachedProviders.providers);
                this.currentProvider = cachedProviders.current;
                
                // If we have a saved provider, select it
                const savedProvider = localStorage.getItem('is:provider');
                if (savedProvider) {
                    const providerSelect = document.getElementById('providerSelect');
                    if (providerSelect) {
                        providerSelect.value = savedProvider;
                    }
                }
                
                // Refresh cache in background
                this.refreshProvidersCache();
            } else {
                const response = await fetch('/api/model/providers');
                if (!response.ok) {
                    this.updateStatus('Provider unavailable', 'unavailable');
                    throw new Error(`Failed to load providers: ${response.status} ${response.statusText}`);
                }
                const data = await response.json();
                this.renderProviderDropdown(data.providers);
                this.currentProvider = data.current;
                
                // If we have a saved provider, select it
                const savedProvider = localStorage.getItem('is:provider');
                if (savedProvider) {
                    const providerSelect = document.getElementById('providerSelect');
                    if (providerSelect) {
                        providerSelect.value = savedProvider;
                    }
                }
            }
        } catch (error) {
            this.updateStatus('Provider unavailable', 'unavailable');
            if (window.ToastUtil) {
                window.ToastUtil.show(`Failed to load providers: ${error.message}`, 'error');
            }
        }
    }

    async discoverModels(provider) {
        // Check if we have cached models first
        const cachedModels = this.getCachedModels(provider);
        if (cachedModels !== null) {
            this.renderModelDropdown(cachedModels);
            document.getElementById('modelSelect').disabled = false;
            this.updateStatus('Connected', 'connected');
            
            // Refresh cache in background to keep it fresh
            this.refreshModelsCache(provider);
            return;
        }

        // Update status to loading
        this.updateStatus('Loading...', 'loading');
        document.getElementById('modelSelect').disabled = true;
        
        try {
            const response = await fetch(`/api/model/discover/${provider}`);
            if (!response.ok) {
                if (response.status === 400) {
                    const errorText = await response.text();
                    if (errorText.includes('OpenRouter models cannot be discovered locally')) {
                        // For OpenRouter, just enable the model select so user can manually enter a model name
                        this.renderModelDropdown([]);
                        document.getElementById('modelSelect').disabled = false;
                        this.updateStatus('Connected', 'connected');
                        if (window.ToastUtil) {
                            window.ToastUtil.show('OpenRouter models cannot be discovered locally. Please enter a model name.', 'info');
                        }
                    } else {
                        throw new Error(errorText);
                    }
                } else if (response.status === 503) {
                    const errorText = await response.text();
                    throw new Error(errorText);
                } else {
                    throw new Error(`Provider unavailable: ${response.status} ${response.statusText}`);
                }
                return;
            }
            const models = await response.json();
            
            // Cache the models
            this.modelsCache[provider] = {
                data: models,
                timestamp: Date.now()
            };
            
            const cacheKey = `is:modelsCache:${provider}`;
            localStorage.setItem(cacheKey, JSON.stringify({
                data: models,
                timestamp: Date.now()
            }));
            
            this.renderModelDropdown(models);
            document.getElementById('modelSelect').disabled = false;
            this.updateStatus('Connected', 'connected');
        } catch (error) {
            this.updateStatus('Provider unavailable', 'unavailable');
            document.getElementById('modelSelect').disabled = true;
            if (window.ToastUtil) {
                window.ToastUtil.show(`Cannot discover models: ${error.message}`, 'error');
            }
        }
    }

    async switchModel(provider, model) {
        // Store previous model for potential revert
        const previousModel = this.currentModel;
        
        // Optimistic UI update: immediately update UI to show new selection
        const modelSelect = document.getElementById('modelSelect');
        if (modelSelect) {
            modelSelect.value = model;
        }
        
        // Update status dot to show pending state
        this.updateModelStatus('loading');
        
        try {
            const response = await fetch('/api/model/switch', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ provider, model })
            });

            if (response.ok) {
                this.currentProvider = provider;
                this.currentModel = model;
                this.updateStatus('Connected', 'connected');
                this.updateModelStatus('available');
                if (window.ToastUtil) {
                    window.ToastUtil.show(`Model switched to ${model}`, 'success');
                }
                
                // Announce model switch to screen readers
                this.announceToScreenReader(`Model successfully switched to ${model}`);
                
                // Save the selected model in localStorage
                localStorage.setItem('is:model', model);
            } else {
                const errorText = await response.text();
                
                // Revert the UI to the previous model selection
                if (previousModel) {
                    modelSelect.value = previousModel;
                }
                
                this.updateStatus('Provider unavailable', 'unavailable');
                this.updateModelStatus('error');
                if (window.ToastUtil) {
                    window.ToastUtil.show(`Failed to switch model: ${errorText}`, 'error');
                }
            }
        } catch (error) {
            // Revert the UI to the previous model selection
            if (previousModel) {
                modelSelect.value = previousModel;
            }
            
            this.updateStatus('Provider unavailable', 'unavailable');
            this.updateModelStatus('error');
            if (window.ToastUtil) {
                window.ToastUtil.show(`Error switching model: ${error.message}`, 'error');
            }
        }
    }

    renderProviderDropdown(providers) {
        const select = document.getElementById('providerSelect');
        if (select) {
            select.innerHTML = providers
                .map(p => `<option value="${p.value}">${p.name}</option>`)
                .join('');
        }
    }

    renderModelDropdown(models) {
        const select = document.getElementById('modelSelect');
        if (select) {
            if (models && models.length > 0) {
                select.innerHTML = models
                    .map(m => `<option value="${m.id}">${m.name || m.id} ${m.sizeBytes ? `(${this.formatSize(m.sizeBytes)})` : ''}</option>`)
                    .join('');
            } else {
                // For providers like OpenRouter where we can't discover models, allow manual entry
                select.innerHTML = `<option value="">Enter model name</option>`;
            }
        }
    }

    formatSize(bytes) {
        if (!bytes) return '';
        const gb = bytes / 1024 / 1024 / 1024;
        return `${gb.toFixed(1)} GB`;
    }

    updateStatus(text, statusType) {
        const statusDot = document.getElementById('statusDot');
        const statusText = document.getElementById('statusText');
        const statusDotTitle = document.getElementById('statusDot');

        if (statusDot && statusText && statusDotTitle) {
            // Reset classes
            statusDot.className = 'w-3 h-3 rounded-full';
            statusText.className = 'text-xs text-gray-500 dark:text-gray-400';
            statusDotTitle.title = text;
            statusText.textContent = text;
        }

        // Apply status-specific classes
        switch (statusType) {
            case 'connected':
                statusDot.classList.add('bg-emerald-500');
                statusText.classList.add('text-emerald-600', 'dark:text-emerald-400');
                statusDotTitle.title = 'Connected';
                break;
            case 'rate-limited':
                statusDot.classList.add('bg-amber-500');
                statusText.classList.add('text-amber-600', 'dark:text-amber-400');
                statusDotTitle.title = 'Rate limited';
                break;
            case 'unavailable':
                statusDot.classList.add('bg-rose-500');
                statusText.classList.add('text-rose-600', 'dark:text-rose-400');
                statusDotTitle.title = 'Provider unavailable';
                break;
            case 'loading':
                statusDot.classList.add('bg-slate-400');
                statusText.classList.add('text-slate-500', 'dark:text-slate-400');
                statusDotTitle.title = 'Loading...';
                break;
            default:
                statusDot.classList.add('bg-slate-400');
                statusText.classList.add('text-slate-500', 'dark:text-slate-400');
                statusDotTitle.title = 'Unknown status';
        }
    }

    // Update model status dot
    updateModelStatus(statusType) {
        const modelStatusDot = document.getElementById('modelStatusDot');
        if (!modelStatusDot) return;
        
        // Reset classes
        modelStatusDot.className = 'ml-2 w-3 h-3 rounded-full';
        modelStatusDot.setAttribute('data-status', statusType);
        
        // Apply status-specific classes
        switch (statusType) {
            case 'available':
                modelStatusDot.classList.add('bg-emerald-500');
                modelStatusDot.title = 'Model available';
                break;
            case 'error':
                modelStatusDot.classList.add('bg-rose-500');
                modelStatusDot.title = 'Model error';
                break;
            case 'loading':
                modelStatusDot.classList.add('bg-amber-500');
                modelStatusDot.title = 'Model switching...';
                break;
            case 'pending':
                modelStatusDot.classList.add('bg-slate-400');
                modelStatusDot.title = 'Model pending';
                break;
            default:
                modelStatusDot.classList.add('bg-slate-400');
                modelStatusDot.title = 'Model status unknown';
        }
    }

    setupEventListeners() {
        const providerSelect = document.getElementById('providerSelect');
        const modelSelect = document.getElementById('modelSelect');

        // Handle provider change
        providerSelect.addEventListener('change', (e) => {
            const selectedValue = e.target.value;
            if (selectedValue) {
                // Save the selected provider to localStorage
                localStorage.setItem('is:provider', selectedValue);
                this.discoverModels(selectedValue);
            }
        });

        // Handle model change
        modelSelect.addEventListener('change', (e) => {
            const selectedProvider = providerSelect.value;
            const selectedModel = e.target.value;
            if (selectedProvider && selectedModel) {
                this.switchModel(selectedProvider, selectedModel);
            }
        });

        // Load models for the initially selected provider if it exists
        if (providerSelect.value) {
            this.discoverModels(providerSelect.value);
        }
        
        // Initialize model status dot with default state
        this.updateModelStatus('pending');
    }

    showLoading() {
        const switchButton = document.getElementById('switch-model-btn');
        switchButton.disabled = true;
        switchButton.textContent = 'Loading...';
    }

    hideLoading() {
        const switchButton = document.getElementById('switch-model-btn');
        switchButton.disabled = false;
        switchButton.textContent = 'Switch Model';
    }

    showError(message) {
        this.showMessage(message, 'error');
    }

    showSuccess(message) {
        this.showMessage(message, 'success');
    }

    showMessage(message, type) {
        // Create or update a message element
        let messageElement = document.getElementById('model-selector-message');
        if (!messageElement) {
            messageElement = document.createElement('div');
            messageElement.id = 'model-selector-message';
            messageElement.style.marginTop = '10px';
            document.querySelector('.model-selector').appendChild(messageElement);
        }
        
        messageElement.textContent = message;
        messageElement.className = `message-${type}`;
        
        // Add some basic styling
        messageElement.style.padding = '8px';
        messageElement.style.borderRadius = '4px';
        messageElement.style.color = 'white';
        messageElement.style.backgroundColor = type === 'error' ? '#d32f2f' : '#2e7d32';
        
        // Auto-hide success messages after 3 seconds
        if (type === 'success') {
            setTimeout(() => {
                messageElement.style.display = 'none';
            }, 3000);
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

// Initialize
document.addEventListener('DOMContentLoaded', () => {
    const modelSelector = new ModelSelector();
});