// Lightweight message renderer module attached to window
// Internal parser is authoritative; no external Markdown libs required.
window.Messages = (function() {
    // Utility function to format timestamp
    function formatTimestamp(ts) {
        const date = typeof ts === 'number' ? new Date(ts) : new Date(ts);
        return {
            display: date.toLocaleString(),
            iso: date.toISOString()
        };
    }

    // Utility function to copy to clipboard
    async function copyToClipboard(text) {
        if (navigator.clipboard && window.isSecureContext) {
            try {
                await navigator.clipboard.writeText(text);
                return true;
            } catch (err) {
                console.error('Failed to copy to clipboard:', err);
                return false;
            }
        } else {
            // Fallback for non-secure contexts
            const textArea = document.createElement('textarea');
            textArea.value = text;
            textArea.setAttribute('readonly', '');
            textArea.style.cssText = `
                position: absolute;
                left: -9999px;
                font-size: 12px;
            `;
            document.body.appendChild(textArea);
            textArea.select();
            try {
                const success = document.execCommand('copy');
                document.body.removeChild(textArea);
                return success;
            } catch (err) {
                console.error('Fallback copy method failed:', err);
                document.body.removeChild(textArea);
                return false;
            }
        }
    }

    // Simple markdown parser
    function parseMarkdown(text) {
        // Split into lines
        const lines = text.split('\n');
        let result = '';
        let inCodeBlock = false;
        let inList = false;
        let inBlockquote = false;
        
        for (let i = 0; i < lines.length; i++) {
            const line = lines[i];
            
            // Check for code block start/end
            if (/^```/.test(line)) {
                if (inCodeBlock) {
                    // End code block
                    result += '</pre></div>\n';
                    inCodeBlock = false;
                } else {
                    // Start code block
                    const langMatch = /^```(\w+)/.exec(line);
                    const lang = langMatch ? langMatch[1] : '';
                    result += `<div class="flex items-center justify-between mb-2">
                        <span class="text-[11px] px-2 py-0.5 rounded bg-neutral-100 text-neutral-600 dark:bg-neutral-800 dark:text-neutral-300">${lang}</span>
                        <button class="code-copy-btn text-xs px-2 py-1 rounded border border-neutral-200 dark:border-neutral-700 hover:bg-neutral-100 dark:hover:bg-neutral-800 transition" title="Copy code">Copy</button>
                    </div>
                    <pre class="bg-neutral-50 dark:bg-neutral-900 border border-neutral-200 dark:border-neutral-800 rounded-lg overflow-auto"><code class="language-${lang}">`;
                    inCodeBlock = true;
                }
                continue;
            }
            
            if (inCodeBlock) {
                result += line + '\n';
                continue;
            }
            
            // Check for blockquote
            if (/^> /.test(line)) {
                if (!inBlockquote) {
                    if (inList) {
                        result += '</ul>\n';
                        inList = false;
                    }
                    result += '<blockquote class="border-l-4 border-neutral-300 pl-4 italic my-2">';
                    inBlockquote = true;
                }
                result += line.substring(2) + ' '; // Remove '> ' and add space
                continue;
            } else if (inBlockquote) {
                result += '</blockquote>\n';
                inBlockquote = false;
            }
            
            // Check for list item
            if (/^[-*] /.test(line)) {
                if (!inList) {
                    if (inBlockquote) {
                        result += '</blockquote>\n';
                        inBlockquote = false;
                    }
                    result += '<ul class="list-disc list-inside my-2">';
                    inList = true;
                }
                result += '<li>' + parseInlineMarkdown(line.substring(2)) + '</li>';
                continue;
            } else if (inList && line.trim() === '') {
                result += '</ul>\n';
                inList = false;
            } else if (inList) {
                result += '</ul>\n';
                inList = false;
            }
            
            // Handle paragraphs and inline markdown
            if (line.trim() === '') {
                if (inList) {
                    result += '</ul>\n';
                    inList = false;
                }
                continue;
            }
            
            if (inList) {
                result += '</ul>\n';
                inList = false;
            }
            
            result += '<p>' + parseInlineMarkdown(line) + '</p>\n';
        }
        
        // Close any open blocks
        if (inCodeBlock) {
            result += '</pre></div>\n';
        }
        if (inList) {
            result += '</ul>\n';
        }
        if (inBlockquote) {
            result += '</blockquote>\n';
        }
        
        return result;
    }

    // Parse inline markdown (links, code, etc.)
    function parseInlineMarkdown(text) {
        // Handle inline code
        text = text.replace(/`([^`]+)`/g, '<code class="bg-neutral-100 dark:bg-neutral-800 rounded px-1.5 py-0.5 font-mono text-sm">$1</code>');
        
        // Handle links
        text = text.replace(/\[([^\]]+)\]\(([^)]+)\)/g, '<a href="$2" class="text-blue-600 hover:underline" target="_blank">$1</a>');
        
        return text;
    }

    // IntersectionObserver for deferred syntax highlighting
    let codeBlockObserver = null;
    let pendingIdleCallback = null;
    
    function initCodeBlockObserver() {
        if (!codeBlockObserver && typeof IntersectionObserver !== 'undefined') {
            codeBlockObserver = new IntersectionObserver((entries) => {
                entries.forEach(entry => {
                    if (entry.isIntersecting) {
                        const codeBlock = entry.target;
                        if (codeBlock.hasAttribute('data-needs-highlight')) {
                            if (typeof Prism !== 'undefined') {
                                Prism.highlightElement(codeBlock.querySelector('code'));
                                codeBlock.removeAttribute('data-needs-highlight');
                                codeBlock.classList.remove('language-unhighlighted');
                            }
                            codeBlockObserver.unobserve(codeBlock);
                        }
                    }
                });
            }, {
                rootMargin: '200px' // Start highlighting when 200px before entering viewport
            });
        }
    }
    
    // Function to process remaining visible code blocks when browser is idle
    function processIdleHighlights() {
        if (typeof requestIdleCallback !== 'undefined') {
            pendingIdleCallback = requestIdleCallback(() => {
                const unhighlighted = document.querySelectorAll('[data-needs-highlight]');
                unhighlighted.forEach(codeBlock => {
                    // Check if the element is visible in the viewport
                    const rect = codeBlock.getBoundingClientRect();
                    const isVisible = rect.top < window.innerHeight && rect.bottom >= 0;
                    
                    if (isVisible && typeof Prism !== 'undefined') {
                        Prism.highlightElement(codeBlock.querySelector('code'));
                        codeBlock.removeAttribute('data-needs-highlight');
                        codeBlock.classList.remove('language-unhighlighted');
                    }
                });
                pendingIdleCallback = null;
            });
        } else {
            // Fallback for browsers that don't support requestIdleCallback
            setTimeout(() => {
                const unhighlighted = document.querySelectorAll('[data-needs-highlight]');
                unhighlighted.forEach(codeBlock => {
                    // Check if the element is visible in the viewport
                    const rect = codeBlock.getBoundingClientRect();
                    const isVisible = rect.top < window.innerHeight && rect.bottom >= 0;
                    
                    if (isVisible && typeof Prism !== 'undefined') {
                        Prism.highlightElement(codeBlock.querySelector('code'));
                        codeBlock.removeAttribute('data-needs-highlight');
                        codeBlock.classList.remove('language-unhighlighted');
                    }
                });
            }, 1);
        }
    }
    
    // Render a single message
    function renderMessage(message) {
        const messageElement = document.createElement('div');
        messageElement.setAttribute('role', 'article');
        
        // Set aria-label based on role and model if available
        if (message.role === 'assistant') {
            const modelInfo = message.model ? ` (Model: ${message.model})` : '';
            messageElement.setAttribute('aria-label', `Assistant message${modelInfo}`);
        } else if (message.role === 'user') {
            messageElement.setAttribute('aria-label', 'User message');
        }
        
        messageElement.setAttribute('data-id', message.id);
        
        // Determine classes based on role
        let bubbleClasses;
        if (message.role === 'assistant') {
            bubbleClasses = 'rounded-xl p-4 bg-white border border-neutral-200 shadow-sm dark:bg-neutral-900 dark:border-neutral-800';
        } else if (message.role === 'user') {
            bubbleClasses = 'rounded-xl p-4 bg-indigo-600 text-white shadow-sm';
        } else {
            bubbleClasses = 'rounded-xl p-4 bg-white border border-neutral-200 shadow-sm dark:bg-neutral-900 dark:border-neutral-800';
        }
        
        // Format timestamp
        const timestamp = formatTimestamp(message.timestamp);
        
        // Create bubble content
        const bubbleElement = document.createElement('div');
        bubbleElement.className = bubbleClasses + ' text-sm md:text-base leading-6 md:leading-7';
        if (message.role === 'assistant') {
            bubbleElement.setAttribute('aria-busy', 'false');
        }
        
        // Parse and set content
        bubbleElement.innerHTML = parseMarkdown(message.content);
        
        messageElement.appendChild(bubbleElement);
        
        // Add timestamp
        const timeElement = document.createElement('time');
        timeElement.className = 'text-xs text-neutral-500 dark:text-neutral-400';
        timeElement.title = timestamp.iso;
        timeElement.textContent = timestamp.display;
        messageElement.appendChild(timeElement);
        
        // Add toolbar for assistant messages
        if (message.role === 'assistant') {
            const toolbar = document.createElement('div');
            toolbar.className = 'mt-2 flex items-center gap-2';
            
            // Copy button
            const copyBtn = document.createElement('button');
            copyBtn.id = `msg-copy-${message.id}`;
            copyBtn.className = 'text-xs px-2 py-1 rounded border border-neutral-200 dark:border-neutral-700 hover:bg-neutral-100 dark:hover:bg-neutral-800 transition';
            copyBtn.setAttribute('aria-label', 'Copy message');
            copyBtn.title = 'Copy message';
            copyBtn.textContent = 'Copy';
            copyBtn.addEventListener('click', function() {
                copyToClipboard(message.content);
            });
            
            // Regenerate button
            const regenerateBtn = document.createElement('button');
            regenerateBtn.className = 'text-xs px-2 py-1 rounded border border-neutral-200 dark:border-neutral-700 hover:bg-neutral-100 dark:hover:bg-neutral-800 transition';
            regenerateBtn.setAttribute('aria-label', 'Regenerate message');
            regenerateBtn.title = 'Regenerate message';
            regenerateBtn.textContent = 'Regenerate';
            regenerateBtn.addEventListener('click', function() {
                window.dispatchEvent(new CustomEvent('ui:regenerate', { detail: { id: message.id } }));
            });
            
            // Delete button
            const deleteBtn = document.createElement('button');
            deleteBtn.className = 'text-xs px-2 py-1 rounded border border-neutral-200 dark:border-neutral-700 hover:bg-neutral-100 dark:hover:bg-neutral-800 transition';
            deleteBtn.setAttribute('aria-label', 'Delete message');
            deleteBtn.title = 'Delete message';
            deleteBtn.textContent = 'Delete';
            deleteBtn.addEventListener('click', function() {
                deleteMessage(message.id);
                window.dispatchEvent(new CustomEvent('ui:delete-message', { detail: { id: message.id } }));
            });
            
            toolbar.appendChild(copyBtn);
            toolbar.appendChild(regenerateBtn);
            toolbar.appendChild(deleteBtn);
            
            messageElement.appendChild(toolbar);
        }
        
        // Handle code copy buttons (need to attach after element is created)
        setTimeout(() => {
            const copyButtons = messageElement.querySelectorAll('.code-copy-btn');
            copyButtons.forEach(btn => {
                btn.setAttribute('aria-label', 'Copy code block');
                btn.addEventListener('click', function() {
                    const codeElement = this.closest('.flex').nextElementSibling.querySelector('code');
                    if (codeElement) {
                        copyToClipboard(codeElement.textContent);
                    }
                });
            });
            
            // Set aria-hidden on language badges
            const langBadges = messageElement.querySelectorAll('.text-[11px].px-2.py-0.5.rounded');
            langBadges.forEach(badge => {
                badge.setAttribute('aria-hidden', 'true');
            });
            
            // Set up deferred syntax highlighting for code blocks
            const codeBlocks = messageElement.querySelectorAll('pre code');
            codeBlocks.forEach(codeElement => {
                const preElement = codeElement.parentElement;
                if (preElement && typeof Prism !== 'undefined') {
                    // Mark code block as needing highlight
                    preElement.setAttribute('data-needs-highlight', 'true');
                    preElement.classList.add('language-unhighlighted');
                    
                    // Initialize observer if not already done
                    initCodeBlockObserver();
                    
                    // Observe the code block
                    codeBlockObserver.observe(preElement);
                }
            });
            
            // Schedule idle callback to process any remaining visible code blocks
            setTimeout(() => {
                processIdleHighlights();
            }, 100);
        }, 0);
        
        return messageElement;
    }

    // Append a message to the chat log
    function appendMessage(message) {
        const chatLog = document.getElementById('chatLog');
        if (chatLog) {
            // Remove welcome message if it exists
            const welcomeMessage = chatLog.querySelector('#welcomeMessage');
            if (welcomeMessage) {
                welcomeMessage.remove();
            }
            
            const messageElement = renderMessage(message);
            chatLog.appendChild(messageElement);
            
            // Scroll to bottom
            chatLog.parentElement.scrollTop = chatLog.parentElement.scrollHeight;
        }
    }

    // Update message content
    function updateMessageContent(id, newContent) {
        const messageElement = document.querySelector(`[data-id="${id}"]`);
        if (messageElement) {
            // Find the bubble element (first child)
            const bubbleElement = messageElement.firstElementChild;
            if (bubbleElement) {
                // Update content
                bubbleElement.innerHTML = parseMarkdown(newContent);
                
                // Re-highlight syntax
                if (typeof Prism !== 'undefined') {
                    requestAnimationFrame(() => {
                        Prism.highlightAllUnder(bubbleElement);
                    });
                }
                
                // Reattach code copy button listeners
                const copyButtons = bubbleElement.querySelectorAll('.code-copy-btn');
                copyButtons.forEach(btn => {
                    btn.addEventListener('click', function() {
                        const codeElement = this.closest('.flex').nextElementSibling.querySelector('code');
                        if (codeElement) {
                            copyToClipboard(codeElement.textContent);
                        }
                    });
                });
            }
        }
    }

    // Delete a message
    function deleteMessage(id) {
        const messageElement = document.querySelector(`[data-id="${id}"]`);
        if (messageElement) {
            messageElement.remove();
        }
    }

    // Demo function to seed messages
    function demoSeed(count = 3) {
        for (let i = 0; i < count; i++) {
            // Add user message
            appendMessage({
                id: `demo-user-${i}`,
                role: 'user',
                content: `This is a demo user message ${i+1}.`,
                timestamp: Date.now() - (count - i) * 60000
            });
            
            // Add assistant message with code
            appendMessage({
                id: `demo-assistant-${i}`,
                role: 'assistant',
                content: `This is a demo assistant response ${i+1}. Here's some code:\n\n```javascript\nfunction helloWorld() {\n  console.log('Hello, world!');\n}\n```\n\nAnd here's a [link](https://example.com) for more information.`,
                timestamp: Date.now() - (count - i) * 60000 + 2000
            });
        }
    }

    // Listen for ui:open-thread events
    window.addEventListener('ui:open-thread', function(event) {
        // Clear chat log
        const chatLog = document.getElementById('chatLog');
        if (chatLog) {
            chatLog.innerHTML = '';
        }
        // In a real implementation, we'd load messages from storage
        // For now, we'll just demonstrate the event handling
    });

    // Virtualization implementation
    let messagesStore = [];  // Internal store of all messages
    const MAX_VISIBLE_MESSAGES = 200;  // Limit visible messages
    const OVERSCAN = 20;  // Extra messages for smooth scrolling
    let messageWindowStart = 0;  // Start index of current window
    let messageWindowEnd = 0;    // End index of current window
    let chatLogElement = null;   // Cache the chat log element
    let loadMoreButton = null;   // "Load older messages" button
    let isInitialRender = true;  // Flag for initial render

    // Function to create the "Load older messages" button
    function createLoadMoreButton() {
        const button = document.createElement('button');
        button.id = 'load-more-messages';
        button.className = 'w-full py-3 px-4 text-center text-sm font-medium text-neutral-600 dark:text-neutral-400 bg-neutral-100 dark:bg-neutral-800 hover:bg-neutral-200 dark:hover:bg-neutral-700 rounded-lg transition mb-2';
        button.textContent = 'Load older messages';
        button.addEventListener('click', loadMoreMessages);
        return button;
    }

    // Function to load more messages by shifting the window up
    function loadMoreMessages() {
        const pageSize = 100;
        const newStart = Math.max(0, messageWindowStart - pageSize);
        
        if (newStart === messageWindowStart) {
            // Already at the beginning
            if (loadMoreButton) {
                loadMoreButton.remove();
                loadMoreButton = null;
            }
            return;
        }
        
        // Preserve scroll position
        const chatLog = document.getElementById('chatLog');
        if (!chatLog) return;
        
        const oldScrollTop = chatLog.parentElement.scrollTop;
        const oldScrollHeight = chatLog.parentElement.scrollHeight;
        
        messageWindowStart = newStart;
        messageWindowEnd = Math.min(messageWindowStart + MAX_VISIBLE_MESSAGES, messagesStore.length);
        
        renderMessageWindow();
        
        // Restore scroll position relative to the bottom
        const newScrollHeight = chatLog.parentElement.scrollHeight;
        chatLog.parentElement.scrollTop = oldScrollTop + (newScrollHeight - oldScrollHeight);
    }

    // Function to render the current window of messages
    function renderMessageWindow() {
        const chatLog = document.getElementById('chatLog');
        if (!chatLog) return;
        
        // Clear the chat log
        chatLog.innerHTML = '';
        
        // Add "Load older messages" button if there are older messages
        if (messageWindowStart > 0 && messagesStore.length > 0) {
            if (!loadMoreButton) {
                loadMoreButton = createLoadMoreButton();
            }
            chatLog.appendChild(loadMoreButton);
        }
        
        // Render visible messages in the current window
        for (let i = messageWindowStart; i < messageWindowEnd; i++) {
            if (i >= 0 && i < messagesStore.length) {
                const messageElement = renderMessage(messagesStore[i]);
                chatLog.appendChild(messageElement);
            }
        }
        
        // Remove welcome message if it exists
        const welcomeMessage = chatLog.querySelector('#welcomeMessage');
        if (welcomeMessage) {
            welcomeMessage.remove();
        }
    }

    // Function to set all thread messages (hydrates the store and renders window)
    function setThreadMessages(messagesArray) {
        messagesStore = messagesArray;
        messageWindowStart = Math.max(0, messagesStore.length - MAX_VISIBLE_MESSAGES);
        messageWindowEnd = messagesStore.length;
        renderMessageWindow();
    }

    // Enhanced appendMessage to work with virtualization
    function appendMessage(message) {
        // Add to store
        messagesStore.push(message);
        
        const chatLog = document.getElementById('chatLog');
        if (!chatLog) return;
        
        // If we're at the end of the current window, append the message
        if (messageWindowEnd === messagesStore.length) {
            // If window is full, shift it forward
            if (messageWindowEnd - messageWindowStart >= MAX_VISIBLE_MESSAGES) {
                messageWindowStart++;
                messageWindowEnd++;
            }
            
            // Append the new message element
            const messageElement = renderMessage(message);
            chatLog.appendChild(messageElement);
            
            // Scroll to bottom
            chatLog.parentElement.scrollTop = chatLog.parentElement.scrollHeight;
        } else {
            // If not at the end, just update the store and re-render the window
            renderMessageWindow();
        }
    }

    // Enhanced updateMessageContent to work with virtualization
    function updateMessageContent(id, newContent) {
        // Update in store
        const messageIndex = messagesStore.findIndex(msg => msg.id === id);
        if (messageIndex !== -1) {
            messagesStore[messageIndex].content = newContent;
            
            // Check if the message is in the current window
            if (messageIndex >= messageWindowStart && messageIndex < messageWindowEnd) {
                const messageElement = document.querySelector(`[data-id="${id}"]`);
                if (messageElement) {
                    // Find the bubble element (first child)
                    const bubbleElement = messageElement.firstElementChild;
                    if (bubbleElement) {
                        // Update content
                        bubbleElement.innerHTML = parseMarkdown(newContent);
                        
                        // Re-highlight syntax if needed
                        const codeBlocks = bubbleElement.querySelectorAll('pre code');
                        codeBlocks.forEach(codeElement => {
                            const preElement = codeElement.parentElement;
                            if (preElement) {
                                // Mark code block as needing highlight
                                preElement.setAttribute('data-needs-highlight', 'true');
                                preElement.classList.add('language-unhighlighted');
                                
                                // Initialize observer if not already done
                                initCodeBlockObserver();
                                
                                // Observe the code block
                                codeBlockObserver.observe(preElement);
                            }
                        });
                        
                        // Reattach code copy button listeners
                        const copyButtons = bubbleElement.querySelectorAll('.code-copy-btn');
                        copyButtons.forEach(btn => {
                            btn.addEventListener('click', function() {
                                const codeElement = this.closest('.flex').nextElementSibling.querySelector('code');
                                if (codeElement) {
                                    copyToClipboard(codeElement.textContent);
                                }
                            });
                        });
                    }
                }
            }
        }
    }

    // Enhanced deleteMessage to work with virtualization
    function deleteMessage(id) {
        // Remove from store
        const originalLength = messagesStore.length;
        messagesStore = messagesStore.filter(msg => msg.id !== id);
        
        // If the message was in the current window, re-render
        if (originalLength !== messagesStore.length) {
            // Adjust window indices if necessary
            if (messageWindowStart >= messagesStore.length) {
                messageWindowStart = Math.max(0, messagesStore.length - MAX_VISIBLE_MESSAGES);
                messageWindowEnd = messagesStore.length;
            } else if (messageWindowEnd > messagesStore.length) {
                messageWindowEnd = messagesStore.length;
            }
            
            renderMessageWindow();
        }
    }

    // Expose functions
    return {
        renderMessage: renderMessage,
        appendMessage: appendMessage,
        updateMessageContent: updateMessageContent,
        deleteMessage: deleteMessage,
        demoSeed: demoSeed,
        // Virtualization functions
        setThreadMessages: setThreadMessages,
        // Expose internal parser for external use
        parseMarkdown: parseMarkdown
    };
};
})();