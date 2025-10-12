// wwwroot/js/ui/toast.js
class Toast {
    constructor() {
        this.container = null;
    }

    initContainer() {
        // Check if container already exists
        this.container = document.getElementById('toast-container');
        if (!this.container) {
            this.container = document.createElement('div');
            this.container.id = 'toast-container';
            this.container.className = 'fixed top-4 right-4 z-50 space-y-2 max-w-xs';
            document.body.appendChild(this.container);
        }
    }

    show(message, type = 'info', duration = 3000) {
        const toast = document.createElement('div');
        toast.className = `
            transform transition-all duration-300 ease-in-out
            p-4 rounded-lg shadow-lg text-white
            flex items-start space-x-2
            opacity-0 translate-y-2
        `;

        // Set background color based on type
        switch (type) {
            case 'success':
                toast.classList.add('bg-emerald-500');
                break;
            case 'error':
                toast.classList.add('bg-rose-500');
                break;
            case 'info':
            default:
                toast.classList.add('bg-blue-500');
                break;
        }

        // Add icon based on type
        let icon = '';
        switch (type) {
            case 'success':
                icon = '<svg class="w-5 h-5 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.707-9.293a1 1 0 00-1.414-1.414L9 10.586 7.707 9.293a1 1 0 00-1.414 1.414l2 2a1 1 0 001.414 0l4-4z" clip-rule="evenodd" /></svg>';
                break;
            case 'error':
                icon = '<svg class="w-5 h-5 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M10 18a8 8 0 100-16 8 8 0 000 16zM8.707 7.293a1 1 0 00-1.414 1.414L8.586 10l-1.293 1.293a1 1 0 101.414 1.414L10 11.414l1.293 1.293a1 1 0 001.414-1.414L11.414 10l1.293-1.293a1 1 0 00-1.414-1.414L10 8.586 8.707 7.293z" clip-rule="evenodd" /></svg>';
                break;
            case 'info':
            default:
                icon = '<svg class="w-5 h-5 flex-shrink-0" fill="currentColor" viewBox="0 0 20 20"><path fill-rule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7-4a1 1 0 11-2 0 1 1 0 012 0zM9 9a1 1 0 000 2v3a1 1 0 001 1h1a1 1 0 100-2v-3a1 1 0 00-1-1H9z" clip-rule="evenodd" /></svg>';
                break;
        }

        toast.innerHTML = `
            <div class="flex items-start">
                ${icon}
                <div class="ml-2">${message}</div>
            </div>
        `;

        this.container.appendChild(toast);

        // Trigger the entrance animation
        setTimeout(() => {
            toast.classList.remove('opacity-0', 'translate-y-2');
            toast.classList.add('opacity-100', 'translate-y-0');
        }, 10);

        // Auto-remove after duration
        if (duration > 0) {
            setTimeout(() => {
                this.remove(toast);
            }, duration);
        }

        return toast;
    }

    remove(toast) {
        // Add exit animation
        toast.classList.remove('opacity-100', 'translate-y-0');
        toast.classList.add('opacity-0', 'translate-y-2');

        // Remove after animation completes
        setTimeout(() => {
            if (toast.parentNode) {
                toast.parentNode.removeChild(toast);
            }
        }, 300);
    }
}

// Initialize Toast only after DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    // Create a global instance
    window.ToastUtil = new Toast();
    window.ToastUtil.initContainer();
});