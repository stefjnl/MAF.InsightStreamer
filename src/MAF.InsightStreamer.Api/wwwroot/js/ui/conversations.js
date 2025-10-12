// VirtualList class for rendering conversation list with virtualization
class VirtualList {
  constructor(container, items, renderItem, itemHeight = 56) {
    this.container = container;
    this.items = items;
    this.renderItem = renderItem;
    this.itemHeight = itemHeight;
    this.visibleStart = 0;
    this.visibleEnd = 0;
    this.scrollHandler = null;
    this.resizeHandler = null;

    this.init();
  }

  init() {
    // Create the container structure
    this.container.innerHTML = '';
    this.spacerDiv = document.createElement('div');
    this.contentDiv = document.createElement('div');
    
    this.container.appendChild(this.spacerDiv);
    this.container.appendChild(this.contentDiv);
    
    // Set up scroll and resize handlers with requestAnimationFrame for performance
    this.scrollHandler = () => {
      if (!this.rafId) {
        this.rafId = requestAnimationFrame(() => {
          this.updateVisibleItems();
          this.rafId = null;
        });
      }
    };

    this.resizeHandler = () => {
      if (!this.rafId) {
        this.rafId = requestAnimationFrame(() => {
          this.updateVisibleItems();
          this.rafId = null;
        });
      }
    };

    this.container.addEventListener('scroll', this.scrollHandler, { passive: true });
    window.addEventListener('resize', this.resizeHandler, { passive: true });

    // Initial render
    this.updateVisibleItems();
  }

  updateVisibleItems() {
    // Read all measurements first to avoid layout thrashing
    const scrollTop = this.container.scrollTop;
    const containerHeight = this.container.clientHeight;
    const totalItems = this.items.length;
    const itemHeight = this.itemHeight;

    // Calculate visible range with some buffer
    const bufferSize = 5; // Show 5 extra items above/below viewport
    const visibleStart = Math.max(0, Math.floor(scrollTop / itemHeight) - bufferSize);
    const visibleEnd = Math.min(
      totalItems,
      Math.ceil((scrollTop + containerHeight) / itemHeight) + bufferSize
    );

    // Update spacer height to reflect total list height
    this.spacerDiv.style.height = `${totalItems * itemHeight}px`;
    
    // Update container transform
    this.contentDiv.style.transform = `translateY(${visibleStart * itemHeight}px)`;
    
    // Clear current content and render visible items
    this.contentDiv.innerHTML = '';

    for (let i = visibleStart; i < visibleEnd; i++) {
      if (i >= 0 && i < totalItems) {
        const itemElement = this.renderItem(this.items[i], i);
        this.contentDiv.appendChild(itemElement);
      }
    }
    
    // Update instance properties after all DOM operations
    this.visibleStart = visibleStart;
    this.visibleEnd = visibleEnd;
  }

  updateItems(newItems) {
    this.items = newItems;
    this.updateVisibleItems();
  }

  destroy() {
    if (this.rafId) {
      cancelAnimationFrame(this.rafId);
    }
    this.container.removeEventListener('scroll', this.scrollHandler);
    window.removeEventListener('resize', this.resizeHandler);
  }
}

// Data adapter for IndexedDB with localStorage fallback
class ConversationsDataAdapter {
  constructor() {
    this.storageKey = 'is:threads';
    this.dbAdapter = window.IndexedDBConversationsAdapter;
    this.inMemoryData = [];
  }

  async load() {
    try {
      // Try to load from IndexedDB first
      if (this.dbAdapter && typeof this.dbAdapter.getAllThreads === 'function') {
        const dbItems = await this.dbAdapter.getAllThreads();
        if (dbItems.length > 0) {
          this.inMemoryData = dbItems;
          return dbItems;
        }
      }
      
      // Fallback to localStorage
      const stored = localStorage.getItem(this.storageKey);
      if (stored) {
        const localStorageItems = JSON.parse(stored);
        this.inMemoryData = localStorageItems;
        return localStorageItems;
      } else {
        // Use mock data for development
        const items = this.generateMockData();
        await this.save(items);
        this.inMemoryData = items;
        return items;
      }
    } catch (e) {
      console.warn('Failed to load conversations:', e);
      // Return in-memory data if storage fails
      return this.inMemoryData || [];
    }
  }

  generateMockData() {
    const mockData = [
      { id: 'thread-1', title: 'Project Planning Discussion', lastSnippet: 'Let\'s plan the next steps for the project', unread: true },
      { id: 'thread-2', title: 'API Integration Issues', lastSnippet: 'The authentication is failing on the staging server', unread: false },
      { id: 'thread-3', title: 'Database Optimization', lastSnippet: 'We need to optimize the query performance', unread: true },
      { id: 'thread-4', title: 'UI Design Review', lastSnippet: 'The new dashboard layout looks great', unread: false },
      { id: 'thread-5', title: 'Security Audit Findings', lastSnippet: 'Found some vulnerabilities in the upload feature', unread: false },
      { id: 'thread-6', title: 'Performance Testing Results', lastSnippet: 'Load testing shows improvement after optimization', unread: true },
      { id: 'thread-7', title: 'Documentation Updates', lastSnippet: 'Updated the API documentation with new endpoints', unread: false },
      { id: 'thread-8', title: 'Client Feedback Session', lastSnippet: 'The client is happy with the progress so far', unread: false },
      { id: 'thread-9', title: 'Team Retrospective', lastSnippet: 'Discussing what went well in the last sprint', unread: false },
      { id: 'thread-10', title: 'Deployment Strategy', lastSnippet: 'Planning the blue-green deployment approach', unread: true }
    ];
    return mockData;
  }

  async save(items) {
    try {
      // Save to IndexedDB
      if (this.dbAdapter && typeof this.dbAdapter.putThreads === 'function') {
        await this.dbAdapter.putThreads(items);
      } else {
        // Fallback to localStorage only if IndexedDB adapter is not available
        console.warn('IndexedDB adapter not available, saving to localStorage only');
      }
      this.inMemoryData = items;
      
      // Also save to localStorage for development fallback
      try {
        localStorage.setItem(this.storageKey, JSON.stringify(items));
      } catch (e) {
        console.warn('Failed to save conversations to localStorage:', e);
      }
    } catch (e) {
      console.warn('Failed to save conversations to IndexedDB:', e);
      // Fallback to localStorage only
      try {
        localStorage.setItem(this.storageKey, JSON.stringify(items));
        this.inMemoryData = items;
      } catch (e) {
        console.warn('Failed to save conversations to localStorage:', e);
        // Keep in-memory data if storage fails
        this.inMemoryData = items;
      }
    }
  }

  async renameItem(id, newTitle) {
    const items = await this.load();
    const item = items.find(i => i.id === id);
    if (item) {
      item.title = newTitle;
      await this.save(items);
    }
  }

  async archiveItem(id, archived = true) {
    const items = await this.load();
    const item = items.find(i => i.id === id);
    if (item) {
      item.archived = archived;
      await this.save(items);
    }
  }

  async markUnread(id, unread = false) {
    const items = await this.load();
    const item = items.find(i => i.id === id);
    if (item) {
      item.unread = unread;
      await this.save(items);
    }
  }
}

// Main Conversations UI class
class ConversationsUI {
  constructor() {
    this.dataAdapter = new ConversationsDataAdapter();
    this.items = [];
    this.filteredItems = [];
    this.activeIndex = -1; // For keyboard navigation
    this.virtualList = null;
    
    // Initialize UI components
    this.initUI();
  }
  
  // Method to set focus on the container for keyboard navigation
  focus() {
    if (this.container) {
      this.container.focus();
    }
  }

  async initUI() {
    // Get DOM elements
    this.container = document.getElementById('convListContainer');
    this.filterInput = document.getElementById('convFilter');
    
    // Set container attributes for accessibility
    this.container.setAttribute('role', 'listbox');
    this.container.setAttribute('aria-label', 'Conversations');
    
    // Load initial data
    this.items = await this.dataAdapter.load();
    this.filteredItems = [...this.items.filter(item => !item.archived)];
    
    // Set up filter input with debouncing
    this.setupFilterInput();
    
    // Initialize virtual list
    this.virtualList = new VirtualList(
      this.container,
      this.filteredItems,
      this.renderItem.bind(this),
      56 // item height in pixels
    );
    
    // Set up keyboard navigation
    this.setupKeyboardNavigation();
  }

  setupFilterInput() {
    let timeoutId = null;
    
    const handleInput = () => {
      const filterText = this.filterInput.value.toLowerCase().trim();
      
      if (filterText === '') {
        this.filteredItems = this.items.filter(item => !item.archived);
      } else {
        this.filteredItems = this.items.filter(item => 
          !item.archived && 
          (item.title.toLowerCase().includes(filterText) || 
           item.lastSnippet.toLowerCase().includes(filterText))
        );
      }
      
      // Update active index after filtering
      this.activeIndex = this.filteredItems.length > 0 ? 0 : -1;
      
      // Update virtual list
      this.virtualList.updateItems(this.filteredItems);
      this.updateAriaSelection();
    };
    
    // Debounce the input handler
    this.filterInput.addEventListener('input', () => {
      clearTimeout(timeoutId);
      timeoutId = setTimeout(handleInput, 150);
    });
  }

  setupKeyboardNavigation() {
    document.addEventListener('keydown', (event) => {
      // Only handle key events when focus is in the conversation list area
      if (event.target.closest('#convListContainer') ||
          event.target === this.filterInput ||
          document.activeElement === document.body) {
        
        if (event.key === 'ArrowDown') {
          event.preventDefault();
          if (this.filteredItems.length > 0) {
            this.activeIndex = Math.min(this.activeIndex + 1, this.filteredItems.length - 1);
            this.updateAriaSelection();
            this.scrollToActiveItem();
            this.updateAriaActiveDescendant();
          }
        } else if (event.key === 'ArrowUp') {
          event.preventDefault();
          this.activeIndex = Math.max(this.activeIndex - 1, 0);
          this.updateAriaSelection();
          this.scrollToActiveItem();
          this.updateAriaActiveDescendant();
        } else if (event.key === 'Home') {
          event.preventDefault();
          if (this.filteredItems.length > 0) {
            this.activeIndex = 0;
            this.updateAriaSelection();
            this.scrollToActiveItem();
            this.updateAriaActiveDescendant();
          }
        } else if (event.key === 'End') {
          event.preventDefault();
          if (this.filteredItems.length > 0) {
            this.activeIndex = this.filteredItems.length - 1;
            this.updateAriaSelection();
            this.scrollToActiveItem();
            this.updateAriaActiveDescendant();
          }
        } else if (event.key === 'PageDown') {
          event.preventDefault();
          if (this.filteredItems.length > 0) {
            const itemsPerPage = Math.floor(this.container.clientHeight / this.virtualList.itemHeight);
            this.activeIndex = Math.min(this.activeIndex + itemsPerPage, this.filteredItems.length - 1);
            this.updateAriaSelection();
            this.scrollToActiveItem();
            this.updateAriaActiveDescendant();
          }
        } else if (event.key === 'PageUp') {
          event.preventDefault();
          if (this.filteredItems.length > 0) {
            const itemsPerPage = Math.floor(this.container.clientHeight / this.virtualList.itemHeight);
            this.activeIndex = Math.max(this.activeIndex - itemsPerPage, 0);
            this.updateAriaSelection();
            this.scrollToActiveItem();
            this.updateAriaActiveDescendant();
          }
        } else if (event.key === 'Enter') {
          event.preventDefault();
          if (this.activeIndex >= 0 && this.activeIndex < this.filteredItems.length) {
            const item = this.filteredItems[this.activeIndex];
            this.openThread(item.id);
          }
        } else if (event.key === 'Escape' && event.target === this.filterInput) {
          // Clear filter and return focus to listbox
          event.preventDefault();
          this.filterInput.value = '';
          const filterEvent = new Event('input', { bubbles: true });
          this.filterInput.dispatchEvent(filterEvent);
          this.container.focus();
        }
      }
    });
  }

  scrollToActiveItem() {
    if (this.activeIndex >= 0 && this.filteredItems.length > 0) {
      const itemElement = this.container.querySelector(`[data-index="${this.activeIndex}"]`);
      if (itemElement) {
        itemElement.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
      }
    }
  }

  updateAriaSelection() {
    // Remove aria-selected from all items
    this.container.querySelectorAll('[role="option"]').forEach(item => {
      item.setAttribute('aria-selected', 'false');
    });
    
    // Add aria-selected to the active item if it exists
    if (this.activeIndex >= 0 && this.activeIndex < this.filteredItems.length) {
      const activeElement = this.container.querySelector(`[data-index="${this.activeIndex}"]`);
      if (activeElement) {
        activeElement.setAttribute('aria-selected', 'true');
      }
    }
  }
  
  updateAriaActiveDescendant() {
    // Update aria-activedescendant attribute on container to point to active element
    if (this.activeIndex >= 0 && this.activeIndex < this.filteredItems.length) {
      const activeElement = this.container.querySelector(`[data-index="${this.activeIndex}"]`);
      if (activeElement) {
        const elementId = `conv-opt-${this.filteredItems[this.activeIndex].id}`;
        activeElement.setAttribute('id', elementId);
        this.container.setAttribute('aria-activedescendant', elementId);
      }
    } else {
      this.container.removeAttribute('aria-activedescendant');
    }
  }

  renderItem(item, index) {
    const itemElement = document.createElement('div');
    itemElement.className = 'flex items-center justify-between px-3 py-2 rounded-md hover:bg-neutral-100 dark:hover:bg-neutral-800 transition cursor-pointer';
    itemElement.setAttribute('role', 'option');
    itemElement.setAttribute('tabindex', '-1');
    itemElement.setAttribute('data-id', item.id);
    itemElement.setAttribute('data-index', index);
    itemElement.setAttribute('id', `conv-opt-${item.id}`);
    
    // Set aria-selected if this is the active item
    if (index === this.activeIndex) {
      itemElement.setAttribute('aria-selected', 'true');
      itemElement.classList.add('bg-indigo-50', 'dark:bg-indigo-900/30');
    }
    
    // Create content wrapper
    const contentWrapper = document.createElement('div');
    contentWrapper.className = 'flex-1 min-w-0';
    
    // Unread indicator
    if (item.unread) {
      const unreadElement = document.createElement('div');
      unreadElement.className = 'w-2 h-2 rounded-full bg-indigo-500 mr-2 flex-shrink-0';
      contentWrapper.appendChild(unreadElement);
    }
    
    // Title and snippet container
    const textContainer = document.createElement('div');
    textContainer.className = 'min-w-0';
    
    // Title
    const titleElement = document.createElement('div');
    titleElement.className = 'text-sm font-medium text-neutral-900 dark:text-neutral-100 truncate';
    titleElement.textContent = item.title;
    
    // Snippet
    const snippetElement = document.createElement('div');
    snippetElement.className = 'text-xs text-neutral-500 dark:text-neutral-400 truncate mt-1';
    snippetElement.textContent = item.lastSnippet;
    
    textContainer.appendChild(titleElement);
    textContainer.appendChild(snippetElement);
    contentWrapper.appendChild(textContainer);
    
    // Action buttons container (hidden by default, shown on hover)
    const actionsContainer = document.createElement('div');
    actionsContainer.className = 'flex space-x-1 opacity-0 group-hover:opacity-100 transition-opacity';
    
    // Rename button
    const renameButton = document.createElement('button');
    renameButton.className = 'text-xs px-2 py-1 rounded hover:bg-neutral-200 dark:hover:bg-neutral-700 transition';
    renameButton.textContent = 'Rename';
    renameButton.type = 'button';
    renameButton.addEventListener('click', (e) => {
      e.stopPropagation();
      this.renameItem(item.id);
    });
    
    // Archive button
    const archiveButton = document.createElement('button');
    archiveButton.className = 'text-xs px-2 py-1 rounded hover:bg-neutral-200 dark:hover:bg-neutral-700 transition';
    archiveButton.textContent = 'Archive';
    archiveButton.type = 'button';
    archiveButton.addEventListener('click', (e) => {
      e.stopPropagation();
      this.archiveItem(item.id);
    });
    
    actionsContainer.appendChild(renameButton);
    actionsContainer.appendChild(archiveButton);
    
    // Add group class for hover effect
    itemElement.classList.add('group');
    
    // Add click handler to open thread
    itemElement.addEventListener('click', () => {
      this.openThread(item.id);
    });
    
    itemElement.appendChild(contentWrapper);
    itemElement.appendChild(actionsContainer);
    
    return itemElement;
  }

  async openThread(id) {
    // Mark as read
    await this.dataAdapter.markUnread(id, false);
    
    // Update the items array to reflect the change
    this.items = await this.dataAdapter.load();
    this.updateFilteredItems();
    
    // Dispatch custom event
    window.dispatchEvent(new CustomEvent('ui:open-thread', { detail: { id } }));
  }

  async renameItem(id) {
    const item = this.items.find(i => i.id === id);
    if (item) {
      const newTitle = prompt('Enter new conversation name:', item.title);
      if (newTitle !== null && newTitle.trim() !== '') {
        await this.dataAdapter.renameItem(id, newTitle.trim());
        this.items = await this.dataAdapter.load();
        this.updateFilteredItems();
        this.virtualList.updateItems(this.filteredItems);
      }
    }
  }

  async archiveItem(id) {
    await this.dataAdapter.archiveItem(id, true);
    this.items = await this.dataAdapter.load();
    this.updateFilteredItems();
    this.virtualList.updateItems(this.filteredItems);
  }

  updateFilteredItems() {
    const filterText = this.filterInput.value.toLowerCase().trim();
    if (filterText === '') {
      this.filteredItems = this.items.filter(item => !item.archived);
    } else {
      this.filteredItems = this.items.filter(item => 
        !item.archived && 
        (item.title.toLowerCase().includes(filterText) || 
         item.lastSnippet.toLowerCase().includes(filterText))
      );
    }
    
    // Update active index after filtering
    this.activeIndex = this.filteredItems.length > 0 ? 0 : -1;
  }
}

// Initialize conversations UI when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
  new ConversationsUI();
});