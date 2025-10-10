// wwwroot/js/model-selector.js
class ModelSelector {
    constructor() {
        this.currentProvider = null;
        this.currentModel = null;
        this.init();
    }

    async init() {
        await this.loadProviders();
        this.setupEventListeners();
    }

    async loadProviders() {
        const response = await fetch('/api/model/providers');
        const data = await response.json();
        this.renderProviderDropdown(data.providers);
        this.currentProvider = data.current;
    }

    async discoverModels(provider) {
        this.showLoading();
        try {
            const response = await fetch(`/api/model/discover/${provider}`);
            if (!response.ok) {
                throw new Error(`Provider unavailable: ${response.statusText}`);
            }
            const models = await response.json();
            this.renderModelDropdown(models);
        } catch (error) {
            this.showError(`Cannot discover models: ${error.message}`);
        } finally {
            this.hideLoading();
        }
    }

    async switchModel(provider, model) {
        const response = await fetch('/api/model/switch', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ provider, model })
        });

        if (response.ok) {
            this.currentProvider = provider;
            this.currentModel = model;
            this.showSuccess(`Switched to ${model}`);
        } else {
            this.showError('Failed to switch model');
        }
    }

    renderProviderDropdown(providers) {
        const select = document.getElementById('provider-select');
        select.innerHTML = providers
            .map(p => `<option value="${p.value}">${p.name}</option>`)
            .join('');
    }

    renderModelDropdown(models) {
        const select = document.getElementById('model-select');
        select.innerHTML = models
            .map(m => `<option value="${m.id}">${m.name} (${this.formatSize(m.sizeBytes)})</option>`)
            .join('');
    }

    formatSize(bytes) {
        if (!bytes) return '';
        const gb = bytes / 1024 / 1024 / 1024;
        return `${gb.toFixed(1)} GB`;
    }

    setupEventListeners() {
        const providerSelect = document.getElementById('provider-select');
        const modelSelect = document.getElementById('model-select');
        const switchButton = document.getElementById('switch-model-btn');

        providerSelect.addEventListener('change', (e) => {
            this.discoverModels(e.target.value);
        });

        switchButton.addEventListener('click', () => {
            const provider = document.getElementById('provider-select').value;
            const model = document.getElementById('model-select').value;
            if (provider && model) {
                this.switchModel(provider, model);
            }
        });

        // Load models for the initially selected provider
        if (providerSelect.value) {
            this.discoverModels(providerSelect.value);
        }
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
}

// Initialize
const modelSelector = new ModelSelector();