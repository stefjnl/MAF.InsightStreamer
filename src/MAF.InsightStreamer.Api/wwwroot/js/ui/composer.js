// Global Composer controller
window.Composer = (function() {
    let state = {
        text: '',
        attachments: [],
        mode: 'default',
        isStreaming: false
    };
    
    // DOM references
    let composerInput, composerFileInput, attachmentPills, slashMenu, stopBtn;
    let quickChips, modeToggles, attachFilesBtn;
    
    // Configuration
    const config = {
        maxLines: 8,
        sendOnEnter: true, // User-configurable option
        enterToSend: true // true = Enter sends, false = Enter creates new line
    };
    
    // Slash command state
    let slashCommands = [
        { name: 'summarize', description: 'Summarize the content' },
        { name: 'fix-grammar', description: 'Fix grammar and spelling' },
        { name: 'make-concise', description: 'Make content more concise' },
        { name: 'explain', description: 'Explain in simple terms' },
        { name: 'translate', description: 'Translate to another language' },
        { name: 'help', description: 'Show available commands' }
    ];
    
    let activeCommandIndex = -1;
    let filteredCommands = [];
    let slashMenuVisible = false;
    
    // Initialize Composer
    function init() {
        // Get DOM references
        composerInput = document.getElementById('composerInput');
        composerFileInput = document.getElementById('composerFileInput');
        attachmentPills = document.getElementById('attachmentPills');
        slashMenu = document.getElementById('slashMenu');
        stopBtn = document.getElementById('stopBtn');
        
        quickChips = document.querySelectorAll('.quick-chip');
        modeToggles = document.querySelectorAll('.mode-toggle');
        attachFilesBtn = document.getElementById('attachFilesBtn');
        
        // Load user preferences
        loadUserPreferences();
        
        // Set up event listeners
        setupEventListeners();
        
        // Initialize auto-resize
        setupAutoResize();
        
        // Set initial mode
        updateModeDisplay('default');
    }
    
    function loadUserPreferences() {
        // Load user-configurable options from localStorage
        const savedSendOnEnter = localStorage.getItem('composer.sendOnEnter');
        if (savedSendOnEnter !== null) {
            config.sendOnEnter = JSON.parse(savedSendOnEnter);
        }
    }
    
    function saveUserPreferences() {
        // Save user-configurable options to localStorage
        localStorage.setItem('composer.sendOnEnter', JSON.stringify(config.sendOnEnter));
    }
    
    function setupEventListeners() {
        // Quick chips click handlers
        quickChips.forEach(chip => {
            chip.addEventListener('click', function() {
                const chipText = this.getAttribute('data-chip');
                prefillComposer(chipText);
                composerInput.focus();
            });
        });
        
        // Mode toggle handlers
        modeToggles.forEach(toggle => {
            toggle.addEventListener('click', function() {
                const mode = this.getAttribute('data-mode');
                updateMode(mode);
            });
        });
        
        // File attachment handlers
        attachFilesBtn.addEventListener('click', function() {
            composerFileInput.click();
        });
        
        composerFileInput.addEventListener('change', function(e) {
            handleFileSelection(e.target.files);
        });
        
        // Drag and drop handlers for the composer container
        const composerContainer = composerInput.closest('[class*="sticky"]'); // Find the parent sticky container
        if (composerContainer) {
            composerContainer.addEventListener('dragover', function(e) {
                e.preventDefault();
                this.classList.add('ring-2');
                this.classList.add('ring-indigo-500');
                this.classList.add('border-indigo-500');
            });
            
            composerContainer.addEventListener('dragleave', function() {
                this.classList.remove('ring-2');
                this.classList.remove('ring-indigo-500');
                this.classList.remove('border-indigo-500');
            });
            
            composerContainer.addEventListener('drop', function(e) {
                e.preventDefault();
                this.classList.remove('ring-2');
                this.classList.remove('ring-indigo-500');
                this.classList.remove('border-indigo-500');
                handleFileSelection(e.dataTransfer.files);
            });
        }
        
        // Key handlers for composer input
        composerInput.addEventListener('keydown', handleKeyDown);
        composerInput.addEventListener('input', handleInput);
        
        // Stop button handler
        stopBtn.addEventListener('click', function() {
            if (window.Streaming && typeof window.Streaming.stop === 'function') {
                window.Streaming.stop();
            }
        });
        
        // Listen for new chat event
        window.addEventListener('ui:new-chat', function() {
            clear();
            hideSlashMenu();
        });
        
        // Document click to close slash menu
        document.addEventListener('click', function(e) {
            if (!slashMenu.contains(e.target) && e.target !== composerInput) {
                hideSlashMenu();
            }
        });
    }
    
    function setupAutoResize() {
        // Calculate line height for the textarea
        const lineHeight = parseInt(window.getComputedStyle(composerInput).lineHeight) || 20;
        const paddingTop = parseInt(window.getComputedStyle(composerInput).paddingTop) || 8;
        const paddingBottom = parseInt(window.getComputedStyle(composerInput).paddingBottom) || 8;
        const verticalPadding = paddingTop + paddingBottom;
        const maxLines = config.maxLines;
        const maxHeight = (lineHeight * maxLines) + verticalPadding;
        
        // Initial height
        composerInput.style.height = (lineHeight + verticalPadding) + 'px';
        composerInput.style.overflowY = 'hidden';
        
        composerInput.addEventListener('input', function() {
            // Reset height to auto to calculate the real scroll height
            this.style.height = 'auto';
            
            // Calculate scroll height and clamp to max height
            const newHeight = Math.min(this.scrollHeight, maxHeight);
            this.style.height = newHeight + 'px';
            
            // Adjust the overflow property based on whether content exceeds max height
            this.style.overflowY = this.scrollHeight > maxHeight ? 'scroll' : 'hidden';
        });
    }
    
    function handleKeyDown(e) {
        if (slashMenuVisible) {
            handleSlashMenuNavigation(e);
            return;
        }
        
        // Check for Cmd/Ctrl+Enter to create new line (opposite of send behavior)
        if ((e.key === 'Enter' && (e.metaKey || e.ctrlKey)) || (e.key === 'Enter' && e.shiftKey)) {
            e.preventDefault();
            // Insert a new line at cursor position
            const start = composerInput.selectionStart;
            const end = composerInput.selectionEnd;
            const currentValue = composerInput.value;
            const newValue = currentValue.substring(0, start) + '\n' + currentValue.substring(end);
            composerInput.value = newValue;
            
            // Set cursor position after the inserted newline
            composerInput.setSelectionRange(start + 1, start + 1);
            
            // Trigger input event to update UI
            composerInput.dispatchEvent(new Event('input', { bubbles: true }));
            return;
        }
        
        // Check for Enter to send (if configured)
        if (e.key === 'Enter' && !e.shiftKey && !e.metaKey && !e.ctrlKey) {
            if (config.enterToSend) {
                e.preventDefault();
                sendIfValid();
                return;
            }
        }
        
        // Check for slash command trigger at the beginning of the line or after a space
        const cursorPos = composerInput.selectionStart;
        const textBeforeCursor = composerInput.value.substring(0, cursorPos);
        const isAtBeginning = cursorPos === 0;
        const previousChar = isAtBeginning ? '' : textBeforeCursor.charAt(cursorPos - 1);
        
        if (e.key === '/' && (isAtBeginning || previousChar === ' ' || previousChar === '\n')) {
            showSlashMenu();
        }
    }
    
    function handleInput(e) {
        if (slashMenuVisible) {
            // Check if we're still in slash command mode
            const cursorPos = composerInput.selectionStart;
            const textBeforeCursor = composerInput.value.substring(0, cursorPos);
            const lastSlashIndex = Math.max(
                textBeforeCursor.lastIndexOf('/'),
                textBeforeCursor.lastIndexOf(' /'), // Handle after space
                textBeforeCursor.lastIndexOf('\n/') // Handle after newline
            );
            
            if (lastSlashIndex === -1 ||
                (lastSlashIndex > 0 && textBeforeCursor.charAt(lastSlashIndex - 1) !== ' ' && textBeforeCursor.charAt(lastSlashIndex - 1) !== '\n')) {
                // Look for the last slash that follows a space or newline
                const parts = textBeforeCursor.split(/[\s\n]/);
                const lastPart = parts[parts.length - 1];
                if (!lastPart.startsWith('/')) {
                    hideSlashMenu();
                    return;
                }
            }
            
            // Update slash menu based on typed text after slash
            let typedText = '';
            if (lastSlashIndex !== -1) {
                typedText = textBeforeCursor.substring(lastSlashIndex + 1);
            }
            updateSlashMenu(typedText);
        }
    }
    
    function handleSlashMenuNavigation(e) {
        if (!slashMenuVisible) return;
        
        const commandItems = slashMenu.querySelectorAll('[role="menuitem"]');
        
        switch (e.key) {
            case 'ArrowDown':
                e.preventDefault();
                activeCommandIndex = Math.min(activeCommandIndex + 1, commandItems.length - 1);
                updateActiveCommand(commandItems);
                break;
                
            case 'ArrowUp':
                e.preventDefault();
                activeCommandIndex = Math.max(activeCommandIndex - 1, 0);
                updateActiveCommand(commandItems);
                break;
                
            case 'Enter':
                e.preventDefault();
                if (activeCommandIndex >= 0 && commandItems[activeCommandIndex]) {
                    insertCommand(filteredCommands[activeCommandIndex].name);
                }
                break;
                
            case 'Escape':
                e.preventDefault();
                hideSlashMenu();
                break;
                
            default:
                // Let regular typing happen
                break;
        }
    }
    
    function updateActiveCommand(commandItems) {
        commandItems.forEach((item, index) => {
            if (index === activeCommandIndex) {
                item.classList.add('bg-indigo-100', 'dark:bg-indigo-900');
                item.classList.remove('hover:bg-neutral-100', 'dark:hover:bg-neutral-700');
                // Set aria-selected for accessibility
                item.setAttribute('aria-selected', 'true');
            } else {
                item.classList.remove('bg-indigo-100', 'dark:bg-indigo-900');
                item.classList.add('hover:bg-neutral-100', 'dark:hover:bg-neutral-700');
                item.setAttribute('aria-selected', 'false');
            }
        });
    }
    
    function showSlashMenu() {
        slashMenuVisible = true;
        
        // Set slash menu attributes for accessibility
        slashMenu.setAttribute('role', 'menu');
        slashMenu.setAttribute('aria-label', 'Commands');
        slashMenu.setAttribute('aria-expanded', 'true');
        
        // Update textarea attributes when menu is open
        composerInput.setAttribute('aria-controls', 'slashMenu');
        composerInput.setAttribute('aria-expanded', 'true');
        
        slashMenu.classList.remove('hidden');
        slashMenu.classList.add('block');
        
        // Initially show all commands
        filteredCommands = [...slashCommands];
        renderSlashMenu();
        
        // Position the menu near the textarea
        positionSlashMenu();
    }
    
    function updateSlashMenu(filterText) {
        filteredCommands = slashCommands.filter(cmd =>
            cmd.name.toLowerCase().includes(filterText.toLowerCase()) ||
            cmd.description.toLowerCase().includes(filterText.toLowerCase())
        );
        
        renderSlashMenu();
        
        if (filteredCommands.length === 0) {
            // Show "no results" message
            const container = slashMenu.querySelector('div');
            container.innerHTML = '<div class="px-3 py-2 text-sm text-neutral-500 dark:text-neutral-400">No commands found</div>';
        } else {
            slashMenu.classList.remove('hidden');
            slashMenu.classList.add('block');
            positionSlashMenu();
        }
    }
    
    function renderSlashMenu() {
        const container = slashMenu.querySelector('div');
        container.innerHTML = '';
        
        activeCommandIndex = -1;
        
        filteredCommands.forEach((cmd, index) => {
            const item = document.createElement('div');
            item.id = `slash-cmd-${index}`;
            item.className = 'flex flex-col px-3 py-2 text-sm cursor-pointer rounded-md hover:bg-neutral-100 dark:hover:bg-neutral-700';
            item.setAttribute('role', 'menuitem');
            item.setAttribute('tabindex', '-1');
            item.setAttribute('aria-label', `${cmd.name}: ${cmd.description}`);
            
            item.innerHTML = `
                <span class="font-medium text-neutral-900 dark:text-neutral-100">/${cmd.name}</span>
                <span class="text-xs text-neutral-500 dark:text-neutral-400">${cmd.description}</span>
            `;
            
            item.addEventListener('click', () => {
                insertCommand(cmd.name);
            });
            
            item.addEventListener('mouseenter', () => {
                activeCommandIndex = index;
                updateActiveCommand(slashMenu.querySelectorAll('[role="menuitem"]'));
            });
            
            container.appendChild(item);
        });
    }
    
    function positionSlashMenu() {
        const inputRect = composerInput.getBoundingClientRect();
        const menuRect = slashMenu.getBoundingClientRect();
        const scrollX = window.scrollX || window.pageXOffset;
        const scrollY = window.scrollY || window.pageYOffset;
        
        // Position below the input
        slashMenu.style.position = 'fixed';
        slashMenu.style.top = (inputRect.bottom + scrollY + 4) + 'px';
        slashMenu.style.left = (inputRect.left + scrollX) + 'px';
        slashMenu.style.width = inputRect.width + 'px';
    }
    
    function insertCommand(command) {
        const cursorPos = composerInput.selectionStart;
        const textBeforeCursor = composerInput.value.substring(0, cursorPos);
        
        // Find the last slash that we want to replace
        const lastSpaceIndex = textBeforeCursor.lastIndexOf(' ');
        const lastNewlineIndex = textBeforeCursor.lastIndexOf('\n');
        const startSearchIndex = Math.max(lastSpaceIndex, lastNewlineIndex, 0);
        const lastSlashIndex = textBeforeCursor.indexOf('/', startSearchIndex);
        
        if (lastSlashIndex !== -1) {
            // Replace the slash command with the actual command
            const newText = textBeforeCursor.substring(0, lastSlashIndex) + `/${command} ` + composerInput.value.substring(cursorPos);
            composerInput.value = newText;
            
            // Set cursor position after the inserted command
            const newCursorPos = lastSlashIndex + command.length + 2; // +2 for '/' and ' '
            composerInput.setSelectionRange(newCursorPos, newCursorPos);
        } else {
            // Fallback: just insert the command at the cursor position
            const start = composerInput.selectionStart;
            const end = composerInput.selectionEnd;
            const currentValue = composerInput.value;
            const newValue = currentValue.substring(0, start) + `/${command} ` + currentValue.substring(end);
            composerInput.value = newValue;
            
            const newCursorPos = start + command.length + 2; // +2 for '/' and ' '
            composerInput.setSelectionRange(newCursorPos, newCursorPos);
        }
        
        hideSlashMenu();
        composerInput.focus();
    }
    
    function hideSlashMenu() {
        slashMenuVisible = false;
        
        // Update accessibility attributes when hiding menu
        slashMenu.setAttribute('aria-expanded', 'false');
        slashMenu.removeAttribute('aria-activedescendant');
        
        // Update textarea attributes when menu is closed
        composerInput.removeAttribute('aria-controls');
        composerInput.setAttribute('aria-expanded', 'false');
        
        slashMenu.classList.add('hidden');
        slashMenu.classList.remove('block');
        activeCommandIndex = -1;
    }
    
    function prefillComposer(text) {
        const start = composerInput.selectionStart;
        const end = composerInput.selectionEnd;
        const currentValue = composerInput.value;
        
        // Replace selected text or insert at cursor position
        const newValue = currentValue.substring(0, start) + text + currentValue.substring(end);
        composerInput.value = newValue;
        
        // Set cursor position after the inserted text
        const newCursorPos = start + text.length;
        composerInput.setSelectionRange(newCursorPos, newCursorPos);
        
        // Trigger input event to update UI
        composerInput.dispatchEvent(new Event('input', { bubbles: true }));
        
        // Focus on the input
        composerInput.focus();
    }
    
    function handleFileSelection(files) {
        for (let i = 0; i < files.length; i++) {
            const file = files[i];
            addAttachment(file);
        }
        
        // Clear the file input so the same file can be selected again
        composerFileInput.value = '';
    }
    
    function addAttachment(file) {
        // Create attachment object
        const attachment = {
            id: Date.now() + '-' + Math.random().toString(36).substr(2, 9),
            file: file,
            name: file.name,
            size: file.size,
            type: file.type
        };
        
        // Add to state
        state.attachments.push(attachment);
        
        // Create and render attachment pill
        renderAttachmentPill(attachment);
    }
    
    function renderAttachmentPill(attachment) {
        const pill = document.createElement('div');
        pill.className = 'flex items-center gap-1 px-2 py-1 bg-neutral-100 dark:bg-neutral-800 rounded-md text-xs';
        pill.setAttribute('data-attachment-id', attachment.id);
        
        const fileName = document.createElement('span');
        fileName.className = 'text-neutral-700 dark:text-neutral-300 truncate max-w-[120px]';
        fileName.textContent = attachment.name;
        
        const fileSize = document.createElement('span');
        fileSize.className = 'text-neutral-500 dark:text-neutral-400 ml-1';
        fileSize.textContent = `(${formatFileSize(attachment.size)})`;
        
        const removeBtn = document.createElement('button');
        removeBtn.type = 'button';
        removeBtn.className = 'ml-1 text-neutral-500 dark:text-neutral-400 hover:text-red-500';
        removeBtn.setAttribute('aria-label', `Remove ${attachment.name}`);
        removeBtn.innerHTML = '&times;';
        
        removeBtn.addEventListener('click', () => {
            removeAttachment(attachment.id);
        });
        
        pill.appendChild(fileName);
        pill.appendChild(fileSize);
        pill.appendChild(removeBtn);
        
        attachmentPills.appendChild(pill);
    }
    
    function removeAttachment(id) {
        // Remove from state
        state.attachments = state.attachments.filter(attachment => attachment.id !== id);
        
        // Remove from UI
        const pill = document.querySelector(`[data-attachment-id="${id}"]`);
        if (pill) {
            pill.remove();
        }
    }
    
    function formatFileSize(bytes) {
        if (bytes === 0) return '0 Bytes';
        
        const k = 1024;
        const sizes = ['Bytes', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        
        return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    }
    
    function updateMode(mode) {
        state.mode = mode;
        updateModeDisplay(mode);
        
        // Dispatch mode change event
        window.dispatchEvent(new CustomEvent('ui:mode-changed', {
            detail: { mode: mode }
        }));
    }
    
    function updateModeDisplay(mode) {
        // Update button states
        modeToggles.forEach(toggle => {
            const toggleMode = toggle.getAttribute('data-mode');
            if (toggleMode === mode) {
                toggle.classList.remove('bg-neutral-100', 'dark:bg-neutral-800', 'text-neutral-700', 'dark:text-neutral-300');
                toggle.classList.add('bg-blue-600', 'text-white');
            } else {
                toggle.classList.add('bg-neutral-100', 'dark:bg-neutral-800', 'text-neutral-700', 'dark:text-neutral-300');
                toggle.classList.remove('bg-blue-600', 'text-white');
            }
        });
    }
    
    function sendIfValid() {
        const text = composerInput.value.trim();
        
        // Check if streaming is active
        if (window.Streaming && window.Streaming.isStreaming) {
            return; // Don't send if already streaming
        }
        
        if (text.length > 0) {
            // Construct the prompt text with mode hint if system mode is active
            let promptText = text;
            if (state.mode === 'system') {
                promptText = `[SYSTEM MODE] ${text}`;
            }
            
            // Call streaming start with the composed prompt
            if (window.Streaming && typeof window.Streaming.start === 'function') {
                window.Streaming.start(promptText);
            }
            
            // Dispatch event for new message being sent
            window.dispatchEvent(new CustomEvent('ui:new-message'));
            
            // Clear the composer
            clear();
        }
    }
    
    function getState() {
        return {
            text: composerInput.value,
            attachments: [...state.attachments],
            mode: state.mode
        };
    }
    
    function clear() {
        composerInput.value = '';
        
        // Reset textarea height
        const lineHeight = parseInt(window.getComputedStyle(composerInput).lineHeight) || 20;
        const paddingTop = parseInt(window.getComputedStyle(composerInput).paddingTop) || 8;
        const paddingBottom = parseInt(window.getComputedStyle(composerInput).paddingBottom) || 8;
        const verticalPadding = paddingTop + paddingBottom;
        composerInput.style.height = (lineHeight + verticalPadding) + 'px';
        composerInput.style.overflowY = 'hidden';
        
        // Clear attachments
        state.attachments = [];
        attachmentPills.innerHTML = '';
    }
    
    // Public API
    return {
        init: init,
        getState: getState,
        clear: clear,
        
        // Additional public methods for configuration
        setSendOnEnter: function(enabled) {
            config.enterToSend = enabled;
            saveUserPreferences();
        },
        getSendOnEnter: function() {
            return config.enterToSend;
        }
    };
})();

// Initialize Composer when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    window.Composer.init();
});