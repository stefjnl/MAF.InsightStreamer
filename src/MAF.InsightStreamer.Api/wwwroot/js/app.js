// Global variables for conversational Q&A
let currentThreadId = null;  // null for first question
let sessionId = null;        // from analysis response

// Tab switching functionality
function switchTab(tab) {
    const youtubeTab = document.getElementById('youtubeTab');
    const documentTab = document.getElementById('documentTab');
    const youtubeSection = document.getElementById('youtubeSection');
    const documentSection = document.getElementById('documentSection');

    if (tab === 'youtube') {
        // Activate YouTube tab
        youtubeTab.classList.add('button-primary');
        youtubeTab.classList.remove('button-secondary');
        youtubeTab.setAttribute('aria-selected', 'true');
        youtubeTab.setAttribute('tabindex', '0');

        // Deactivate Document tab
        documentTab.classList.add('button-secondary');
        documentTab.classList.remove('button-primary');
        documentTab.setAttribute('aria-selected', 'false');
        documentTab.setAttribute('tabindex', '-1');

        // Show YouTube section
        youtubeSection.classList.remove('hidden');
        youtubeSection.removeAttribute('hidden');
        documentSection.classList.add('hidden');
        documentSection.setAttribute('hidden', 'true');
    } else {
        // Activate Document tab
        documentTab.classList.add('button-primary');
        documentTab.classList.remove('button-secondary');
        documentTab.setAttribute('aria-selected', 'true');
        documentTab.setAttribute('tabindex', '0');

        // Deactivate YouTube tab
        youtubeTab.classList.add('button-secondary');
        youtubeTab.classList.remove('button-primary');
        youtubeTab.setAttribute('aria-selected', 'false');
        youtubeTab.setAttribute('tabindex', '-1');

        // Show Document section
        documentSection.classList.remove('hidden');
        documentSection.removeAttribute('hidden');
        youtubeSection.classList.add('hidden');
        youtubeSection.setAttribute('hidden', 'true');
    }

    // Hide results when switching tabs
    hideResults();
}

async function analyzeVideo() {
    const url = document.getElementById('videoUrl').value;

    // Validate URL
    if (!url || (!url.includes('youtube.com') && !url.includes('youtu.be'))) {
        showError('Please enter a valid YouTube URL');
        return;
    }

    // Show loading, hide previous results
    showLoading('Analyzing video...');

    try {
        const response = await fetch('/api/youtube/summarize', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(url)
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(errorText || 'Analysis failed');
        }

        const result = await response.text();
        displayVideoResults(result);
    } catch (error) {
        showError(error.message);
    } finally {
        hideLoading();
    }
}

async function analyzeDocument() {
    const fileInput = document.getElementById('documentFile');
    const file = fileInput.files[0];

    // Validate file
    if (!file) {
        showError('Please select a document file');
        return;
    }

    // Validate file type
    const allowedTypes = ['.pdf', '.docx', '.md', '.txt'];
    const fileExtension = '.' + file.name.split('.').pop().toLowerCase();
    if (!allowedTypes.includes(fileExtension)) {
        showError('Invalid file type. Please select a PDF, DOCX, MD, or TXT file.');
        return;
    }

    // Validate file size (10MB)
    const maxSize = 10 * 1024 * 1024; // 10MB in bytes
    if (file.size > maxSize) {
        showError('File size exceeds 10MB limit. Please select a smaller file.');
        return;
    }

    // Show loading, hide previous results
    showLoading('Analyzing document...');

    try {
        const formData = new FormData();
        formData.append('file', file);
        formData.append('analysisRequest', 'Provide a concise summary and extract key points');

        const response = await fetch('/api/document/analyze', {
            method: 'POST',
            body: formData
        });

        if (!response.ok) {
            const errorText = await response.text();
            throw new Error(errorText || 'Document analysis failed');
        }

        const result = await response.json();
        displayDocumentResults(result);
    } catch (error) {
        showError(error.message);
    } finally {
        hideLoading();
    }
}

function showLoading(text) {
    document.getElementById('loadingText').textContent = text;
    document.getElementById('loading').classList.remove('hidden');
    document.getElementById('results').classList.add('hidden');
    document.getElementById('error').classList.add('hidden');
}

function hideLoading() {
    document.getElementById('loading').classList.add('hidden');
}

function hideResults() {
    document.getElementById('results').classList.add('hidden');
    document.getElementById('error').classList.add('hidden');
}

function displayVideoResults(result) {
    const resultsTitle = document.getElementById('resultsTitle');
    if (resultsTitle) {
        resultsTitle.textContent = 'Video Analysis Results';
    }
    document.getElementById('documentMetadata').classList.add('hidden');
    // Use markdown utility to render markdown content
    const renderedHtml = window.Markdown.useInternalParser(result);
    document.getElementById('resultsContent').innerHTML = renderedHtml;
    document.getElementById('results').classList.remove('hidden');
}

function displayDocumentResults(result) {
    const resultsTitle = document.getElementById('resultsTitle');
    if (resultsTitle) {
        resultsTitle.textContent = 'Document Analysis Results';
    }

    // Store sessionId for Q&A functionality
    sessionId = result.sessionId;

    // Display document metadata
    const metadata = result.metadata;
    document.getElementById('metaFileName').textContent = metadata.fileName;
    document.getElementById('metaFileType').textContent = metadata.fileType;
    document.getElementById('metaFileSize').textContent = formatFileSize(metadata.fileSizeBytes);
    document.getElementById('metaPageCount').textContent = metadata.pageCount || 'N/A';
    document.getElementById('documentMetadata').classList.remove('hidden');

    // Display analysis results
    let contentHtml = '';

    // Summary section
    if (result.summary) {
        // Use markdown utility to render markdown content
        const renderedSummary = window.Markdown.useInternalParser(result.summary);
        contentHtml += `
            <div class="mb-6">
                <h3 class="text-lg font-semibold text-gray-800 mb-2">Summary</h3>
                <div class="text-gray-700 leading-relaxed">${renderedSummary}</div>
            </div>
        `;
    }

    // Key points section
    if (result.keyPoints && result.keyPoints.length > 0) {
        contentHtml += `
            <div class="mb-6">
                <h3 class="text-lg font-semibold text-gray-800 mb-2">Key Points</h3>
                <ul class="list-disc list-inside space-y-2">
                    ${result.keyPoints.map(point => {
            // Use markdown utility to render markdown content for each point
            const renderedPoint = window.Markdown.useInternalParser(point);
            return `<li class="text-gray-700">${renderedPoint}</li>`;
        }).join('')}
                </ul>
            </div>
        `;
    }

    // Processing info
    contentHtml += `
        <div class="mt-6 pt-4 border-t border-gray-200">
            <p class="text-sm text-gray-500">
                Document processed in ${result.processingTimeMs}ms across ${result.chunkCount} chunks
            </p>
        </div>
    `;

    // Add "Ask Questions About This Document" button
    contentHtml += `
        <div class="mt-6 pt-4 border-t border-gray-200">
            <button
                id="showChatBtn"
                class="button-primary px-6 py-3"
            >
                Ask Questions About This Document
            </button>
        </div>
    `;

    document.getElementById('resultsContent').innerHTML = contentHtml;
    document.getElementById('results').classList.remove('hidden');

    const showChatBtn = document.getElementById('showChatBtn');
    if (showChatBtn) {
        showChatBtn.addEventListener('click', showChatInterface);
    }
}


function formatFileSize(bytes) {
    if (bytes === 0) return '0 Bytes';

    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));

    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

function showError(message) {
    document.getElementById('errorMessage').textContent = message;
    document.getElementById('error').classList.remove('hidden');
}

// Show the chat interface for Q&A
function showChatInterface() {
    document.getElementById('qaSection').classList.remove('hidden');
    // Scroll to the Q&A section
    document.getElementById('qaSection').scrollIntoView({ behavior: 'smooth' });
    // Focus on the composer input at the bottom
    document.getElementById('composerInput').focus();
}

// Start a new conversation (clear threadId)
function startNewConversation() {
    currentThreadId = null;
    // Clear the chat history
    const chatLog = document.getElementById('chatLog');
    if (chatLog) {
        chatLog.innerHTML = `
            <div id="welcomeMessage" class="text-center text-gray-500 py-8">
                Ask a question about the document to get started
            </div>
        `;
    }
    // Hide any error messages
    document.getElementById('qaError').classList.add('hidden');
    // Focus on the composer input at the bottom
    document.getElementById('composerInput').focus();
}

// Handle Enter key in the composer input
function handleComposerKeyPress(event) {
    if (event.key === 'Enter' && !event.shiftKey) {
        event.preventDefault();
        sendQuestion();
    }
}

// Send a question using the streaming controller
function sendQuestion() {
    const questionInput = document.getElementById('questionInput') || document.getElementById('composerInput');
    const question = questionInput.value.trim();

    if (!question) {
        return;
    }

    // Use the streaming controller to handle the request
    if (window.Streaming && typeof window.Streaming.start === 'function') {
        window.Streaming.start(question);
    } else {
        console.warn('Streaming controller not available');
    }

    // Clear the input field after starting the stream
    questionInput.value = '';
}

// Initialize the application
document.addEventListener('DOMContentLoaded', function () {
    // Tab switching
    const youtubeTab = document.getElementById('youtubeTab');
    if (youtubeTab) {
        youtubeTab.addEventListener('click', () => switchTab('youtube'));
    }

    const documentTab = document.getElementById('documentTab');
    if (documentTab) {
        documentTab.addEventListener('click', () => switchTab('document'));
    }

    // Analyze buttons
    const analyzeVideoBtn = document.getElementById('analyzeVideoBtn');
    if (analyzeVideoBtn) {
        analyzeVideoBtn.addEventListener('click', analyzeVideo);
    }

    const analyzeDocumentBtn = document.getElementById('analyzeDocumentBtn');
    if (analyzeDocumentBtn) {
        analyzeDocumentBtn.addEventListener('click', analyzeDocument);
    }

    // Q&A buttons
    const newConversationBtn = document.getElementById('newConversationBtn');
    if (newConversationBtn) {
        newConversationBtn.addEventListener('click', startNewConversation);
    }

    const sendButton = document.getElementById('sendButton');
    if (sendButton) {
        sendButton.addEventListener('click', sendQuestion);
    }

    const questionInput = document.getElementById('questionInput');
    if (questionInput) {
        questionInput.addEventListener('keypress', handleComposerKeyPress);
    }


    // Add click handlers for quick chips
    document.querySelectorAll('.chip[data-chip]').forEach(chip => {
        chip.addEventListener('click', function () {
            const prompt = this.getAttribute('data-chip');
            const composerInput = document.getElementById('composerInput');
            if (composerInput) {
                composerInput.value = prompt;
                composerInput.focus();
            }
        });
    });

    // Add event listener for the new chat button
    const newChatBtn = document.getElementById('newChatBtn');
    if (newChatBtn) {
        newChatBtn.addEventListener('click', function () {
            // Dispatch a custom event for new chat
            window.dispatchEvent(new CustomEvent('ui:new-chat'));
        });
    }

    // Initialize the streaming controller
    if (window.Streaming) {
        window.Streaming.init();
    }

    // Check if chat is empty and show empty prompts if needed
    setTimeout(() => {
        const chatLog = document.getElementById('chatLog');
        if (chatLog && chatLog.children.length <= 1) { // Only if there are no messages or just the welcome message
            if (window.Errors && typeof window.Errors.showEmptyPrompts === 'function') {
                window.Errors.showEmptyPrompts();
            }
        }
    }, 100);

    // Update provider/model selectors with modern classes
    const providerSelect = document.getElementById('providerSelect');
    const modelSelect = document.getElementById('modelSelect');
    if (providerSelect) {
        providerSelect.classList.add('select-modern');
    }
    if (modelSelect) {
        modelSelect.classList.add('select-modern');
    }
});