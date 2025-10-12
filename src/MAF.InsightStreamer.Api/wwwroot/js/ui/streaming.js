// Global Streaming controller for handling assistant response streaming
window.Streaming = (function() {
    // DOM references
    let composerInput;
    let stopBtn;
    let composerThinking;
    let chatLog;
    let composerStatusRow;
    
    // State management
    let abortController = null;
    let isStreaming = false;
    let autoScrollEnabled = true;
    
    // Initialize the streaming controller
    function init() {
        // Get DOM references
        composerInput = document.getElementById('composerInput');
        stopBtn = document.getElementById('stopBtn');
        composerThinking = document.getElementById('composerThinking');
        composerStatusRow = document.getElementById('composerStatusRow');
        chatLog = document.getElementById('chatLog');
        
        if (!composerInput || !stopBtn || !composerThinking || !chatLog) {
            console.error('Required DOM elements not found for streaming');
            return;
        }
        
        // Set role="status" on the composer thinking element for accessibility
        if (composerThinking) {
            composerThinking.setAttribute('role', 'status');
        }
        
        // Attach event listeners
        stopBtn.addEventListener('click', cancel);
        
        // Add key handler for Cmd/Ctrl+Enter
        composerInput.addEventListener('keydown', function(event) {
            if ((event.ctrlKey || event.metaKey) && event.key === 'Enter') {
                event.preventDefault();
                if (!isStreaming && composerInput.value.trim().length > 0) {
                    start(composerInput.value);
                }
            }
        });
        
        // Add selection listeners for scroll-lock
        if (chatLog) {
            chatLog.addEventListener('mouseup', function() {
                // Check if user has made a text selection
                const selection = window.getSelection();
                if (selection && !selection.isCollapsed) {
                    autoScrollEnabled = false;
                }
            });
            
            // Also listen for click to potentially re-enable auto-scroll
            chatLog.addEventListener('click', function() {
                // If user clicks without making a selection, potentially re-enable auto-scroll
                setTimeout(() => {
                    const selection = window.getSelection();
                    if (selection && selection.isCollapsed) {
                        // Check if user is near the bottom of the chat log
                        const isNearBottom = chatLog.parentElement.scrollHeight - chatLog.parentElement.scrollTop <= chatLog.parentElement.clientHeight + 80;
                        if (isNearBottom) {
                            autoScrollEnabled = true;
                        }
                    }
                }, 0);
            });
        }
    }
    
    // Start the assistant response stream
    async function start(promptText) {
        if (isStreaming) return;
        
        // Set streaming state
        isStreaming = true;
        abortController = new AbortController();
        
        try {
            // Add user message first
            const userMessage = {
                id: 'user-' + Date.now(),
                role: 'user',
                content: promptText,
                timestamp: Date.now()
            };
            window.Messages.appendMessage(userMessage);
            
            // Create a new assistant placeholder message
            const messageId = 'assistant-' + Date.now();
            const assistantMessage = {
                id: messageId,
                role: 'assistant',
                content: '',
                timestamp: Date.now()
            };
            
            // Append the message placeholder
            window.Messages.appendMessage(assistantMessage);
            
            // Get the message container and set aria-busy
            const messageContainer = document.querySelector(`[data-id="${messageId}"]`);
            if (messageContainer) {
                const bubbleElement = messageContainer.firstElementChild;
                if (bubbleElement) {
                    bubbleElement.setAttribute('aria-busy', 'true');
                }
                
                // Set aria-busy on chatLog as well
                chatLog.setAttribute('aria-busy', 'true');
            }
            
            // Update UI state to show streaming indicators
            composerThinking.classList.remove('hidden');
            stopBtn.classList.remove('hidden');
            composerInput.disabled = true;
            
            // Prepare request payload
            const requestBody = {
                sessionId: typeof window.sessionId !== 'undefined' ? window.sessionId : null,
                question: promptText,
                threadId: typeof window.currentThreadId !== 'undefined' ? window.currentThreadId : null
            };
            
            // Make the API request
            const response = await fetch('/api/document/ask', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(requestBody),
                signal: abortController.signal
            });
            
            if (!response.ok) {
                throw new Error(`HTTP error! status: ${response.status}`);
            }
            
            let result;
            
            // Check if the response is streaming text or JSON
            const contentType = response.headers.get('content-type');
            if (contentType && contentType.includes('text/plain')) {
                // Handle streaming text response
                const reader = response.body.getReader();
                const decoder = new TextDecoder();
                let buffer = '';
                
                // Buffer chunks and update DOM at ~16ms rAF cadence
                let pendingUpdate = null;
                
                try {
                    while (true) {
                        const { done, value } = await reader.read();
                        
                        if (done) {
                            break;
                        }
                        
                        // Decode the chunk
                        buffer += decoder.decode(value, { stream: true });
                        
                        // Process the buffer for complete chunks
                        const lines = buffer.split('\n');
                        buffer = lines.pop() || ''; // Keep incomplete line in buffer
                        
                        for (const line of lines) {
                            if (line.trim()) {
                                // Schedule update with rAF throttling
                                if (pendingUpdate) {
                                    cancelAnimationFrame(pendingUpdate);
                                }
                                
                                pendingUpdate = requestAnimationFrame(() => {
                                    // Get current content and append new text
                                    const currentMessage = document.querySelector(`[data-id="${messageId}"]`);
                                    if (currentMessage) {
                                        const bubbleElement = currentMessage.firstElementChild;
                                        if (bubbleElement) {
                                            // Update content via messages API
                                            const currentContent = bubbleElement.textContent || bubbleElement.innerText || '';
                                            window.Messages.updateMessageContent(messageId, currentContent + line);
                                            
                                            // Auto-scroll if enabled and user is near bottom
                                            if (autoScrollEnabled) {
                                                const isNearBottom = chatLog.parentElement.scrollHeight - chatLog.parentElement.scrollTop <= chatLog.parentElement.clientHeight + 80;
                                                if (isNearBottom) {
                                                    chatLog.parentElement.scrollTop = chatLog.parentElement.scrollHeight;
                                                }
                                            }
                                        }
                                    }
                                });
                            }
                        }
                    }
                    
                    // Process any remaining buffer
                    if (buffer) {
                        if (pendingUpdate) {
                            cancelAnimationFrame(pendingUpdate);
                        }
                        
                        pendingUpdate = requestAnimationFrame(() => {
                            const currentMessage = document.querySelector(`[data-id="${messageId}"]`);
                            if (currentMessage) {
                                const bubbleElement = currentMessage.firstElementChild;
                                if (bubbleElement) {
                                    const currentContent = bubbleElement.textContent || bubbleElement.innerText || '';
                                    window.Messages.updateMessageContent(messageId, currentContent + buffer);
                                }
                            }
                        });
                    }
                } finally {
                    if (pendingUpdate) {
                        cancelAnimationFrame(pendingUpdate);
                    }
                    reader.releaseLock();
                }
            } else {
                // Handle JSON response (non-streaming fallback)
                result = await response.json();
                
                // Store threadId for subsequent questions
                if (result.threadId || result.ThreadId) {
                    window.currentThreadId = result.threadId || result.ThreadId;
                }
                
                // If conversation history is available, render the full history
                if (result && result.conversationHistory) {
                    // First, clear the chat log of any previous messages (but keep the assistant placeholder)
                    const chatLog = document.getElementById('chatLog');
                    if (chatLog) {
                        // Find all messages that are not the current assistant message
                        const existingMessages = chatLog.querySelectorAll('[data-id]:not([data-id="' + messageId + '"])');
                        existingMessages.forEach(msg => msg.remove());
                    }
                    
                    // Render all messages from the conversation history
                    result.conversationHistory.forEach((msg, index) => {
                        // Convert the message format if needed
                        const messageRole = msg.role === 0 ? 'user' : (msg.role === 1 ? 'assistant' : 'assistant');
                        const message = {
                            id: `${messageRole}-${index}-${Date.now()}`,
                            role: messageRole,
                            content: msg.content || msg.answer || '',
                            timestamp: msg.timestamp || Date.now(),
                            chunkReferences: msg.chunkReferences || msg.RelevantChunkIndices
                        };
                        
                        // Add the message to the chat
                        window.Messages.appendMessage(message);
                    });
                    
                    // If there's no conversation history, update the assistant message content
                    if (result.conversationHistory.length === 0) {
                        // Update the assistant message content
                        window.Messages.updateMessageContent(messageId, result.answer || result.Answer || '');
                    }
                } else {
                    // Fallback: update the assistant message content
                    window.Messages.updateMessageContent(messageId, result.answer || result.Answer || '');
                }
            }
        } catch (error) {
            // Handle errors appropriately
            if (error.name === 'AbortError') {
                // User cancelled - don't show error toast
            } else if (error.message.includes('429')) {
                // Handle rate limit error
                if (window.Toast) {
                    window.Toast.show('Rate limit exceeded', 'You have reached the maximum number of requests. Please try again later.', 'warning');
                }
                
                // Add inline error note in the assistant message
                const currentMessage = document.querySelector(`[data-id="${messageId}"]`);
                if (currentMessage) {
                    const bubbleElement = currentMessage.firstElementChild;
                    if (bubbleElement) {
                        const currentContent = bubbleElement.textContent || bubbleElement.innerText || '';
                        const updatedContent = currentContent + '\n\n⚠️ Rate limit exceeded. Please wait before asking another question.';
                        window.Messages.updateMessageContent(messageId, updatedContent);
                    }
                }
            } else {
                // Handle network/5xx errors
                const currentMessage = document.querySelector(`[data-id="${messageId}"]`);
                if (currentMessage) {
                    const bubbleElement = currentMessage.firstElementChild;
                    if (bubbleElement) {
                        const currentContent = bubbleElement.textContent || bubbleElement.innerText || '';
                        const updatedContent = currentContent + '\n\n⚠️ An error occurred while processing your request. Please try again.';
                        window.Messages.updateMessageContent(messageId, updatedContent);
                    }
                }
                
                // Dispatch a provider error event
                window.dispatchEvent(new CustomEvent('ui:provider-error', { detail: { error: error.message } }));
            }
        } finally {
            // Complete the streaming process
            completeStreaming();
        }
    }
    
    // Cancel the in-flight request
    function cancel() {
        if (abortController) {
            abortController.abort();
        }
    }
    
    // Complete the streaming process and update UI
    function completeStreaming() {
        isStreaming = false;
        
        // Update UI state to idle
        if (composerThinking) composerThinking.classList.add('hidden');
        if (stopBtn) stopBtn.classList.add('hidden');
        if (composerInput) composerInput.disabled = false;
        
        // Set aria-busy to false on active assistant message
        const activeAssistantMessage = document.querySelector('[data-id^="assistant-"]');
        if (activeAssistantMessage) {
            const bubbleElement = activeAssistantMessage.firstElementChild;
            if (bubbleElement) {
                bubbleElement.setAttribute('aria-busy', 'false');
            }
        }
        
        // Set aria-busy to false on chatLog as well
        if (chatLog) {
            chatLog.setAttribute('aria-busy', 'false');
        }
    }
    
    // Public API
    return {
        init: init,
        start: start,
        cancel: cancel
    };
})();