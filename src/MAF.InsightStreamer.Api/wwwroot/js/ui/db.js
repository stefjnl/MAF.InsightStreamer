// IndexedDB helper for conversations with localStorage fallback
class IndexedDBConversationsAdapter {
  constructor() {
    this.dbName = 'isdb';
    this.version = 1;
    this.storeName = 'threads';
    this.db = null;
    this.fallbackToLocalStorage = false;
  }

  async openDb() {
    return new Promise((resolve, reject) => {
      const request = indexedDB.open(this.dbName, this.version);
      
      request.onerror = () => {
        console.warn('IndexedDB unavailable, falling back to localStorage:', request.error);
        this.fallbackToLocalStorage = true;
        resolve(null);
      };
      
      request.onsuccess = () => {
        this.db = request.result;
        resolve(this.db);
      };
      
      request.onupgradeneeded = (event) => {
        const db = event.target.result;
        if (!db.objectStoreNames.contains(this.storeName)) {
          db.createObjectStore(this.storeName, { keyPath: 'id' });
        }
      };
    });
  }

  async getAllThreads() {
    if (this.fallbackToLocalStorage) {
      try {
        const stored = localStorage.getItem('is:threads');
        return stored ? JSON.parse(stored) : [];
      } catch (error) {
        console.warn('Failed to load conversations from localStorage:', error);
        return [];
      }
    }
    
    if (!this.db) {
      await this.openDb();
    }
    
    if (!this.db) return [];
    
    return new Promise((resolve, reject) => {
      const transaction = this.db.transaction([this.storeName], 'readonly');
      const store = transaction.objectStore(this.storeName);
      const request = store.getAll();
      
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => {
        console.warn('Failed to load conversations from IndexedDB:', request.error);
        // Fallback to localStorage
        try {
          const stored = localStorage.getItem('is:threads');
          resolve(stored ? JSON.parse(stored) : []);
        } catch (error) {
          console.warn('Failed to load from localStorage fallback:', error);
          resolve([]);
        }
      };
    });
  }

  async putThreads(threadsArray) {
    if (this.fallbackToLocalStorage) {
      try {
        localStorage.setItem('is:threads', JSON.stringify(threadsArray));
      } catch (error) {
        console.warn('Failed to save conversations to localStorage:', error);
      }
      return;
    }
    
    if (!this.db) {
      await this.openDb();
    }
    
    if (!this.db) {
      // Fallback to localStorage
      try {
        localStorage.setItem('is:threads', JSON.stringify(threadsArray));
      } catch (error) {
        console.warn('Failed to save conversations to localStorage:', error);
      }
      return;
    }
    
    return new Promise((resolve, reject) => {
      const transaction = this.db.transaction([this.storeName], 'readwrite');
      const store = transaction.objectStore(this.storeName);
      
      // Clear existing entries first
      const clearRequest = store.clear();
      
      clearRequest.onsuccess = () => {
        // Add new entries
        let count = 0;
        if (threadsArray.length === 0) {
          resolve();
          return;
        }
        
        for (const thread of threadsArray) {
          const addRequest = store.add(thread);
          addRequest.onsuccess = () => {
            count++;
            if (count === threadsArray.length) {
              resolve();
            }
          };
          addRequest.onerror = () => {
            console.warn('Failed to add thread to IndexedDB:', addRequest.error);
            count++;
            if (count === threadsArray.length) {
              resolve();
            }
          };
        }
      };
      
      clearRequest.onerror = () => {
        console.warn('Failed to clear conversations in IndexedDB:', clearRequest.error);
        // Still try to add the new entries
        let count = 0;
        if (threadsArray.length === 0) {
          resolve();
          return;
        }
        
        for (const thread of threadsArray) {
          const addRequest = store.add(thread);
          addRequest.onsuccess = () => {
            count++;
            if (count === threadsArray.length) {
              resolve();
            }
          };
          addRequest.onerror = () => {
            console.warn('Failed to add thread to IndexedDB:', addRequest.error);
            count++;
            if (count === threadsArray.length) {
              resolve();
            }
          };
        }
      };
    });
  }

  async getThread(id) {
    if (this.fallbackToLocalStorage) {
      try {
        const stored = localStorage.getItem('is:threads');
        if (stored) {
          const threads = JSON.parse(stored);
          return threads.find(thread => thread.id === id);
        }
        return null;
      } catch (error) {
        console.warn('Failed to get thread from localStorage:', error);
        return null;
      }
    }
    
    if (!this.db) {
      await this.openDb();
    }
    
    if (!this.db) return null;
    
    return new Promise((resolve, reject) => {
      const transaction = this.db.transaction([this.storeName], 'readonly');
      const store = transaction.objectStore(this.storeName);
      const request = store.get(id);
      
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => {
        console.warn('Failed to get thread from IndexedDB:', request.error);
        // Fallback to localStorage
        try {
          const stored = localStorage.getItem('is:threads');
          if (stored) {
            const threads = JSON.parse(stored);
            resolve(threads.find(thread => thread.id === id));
          } else {
            resolve(null);
          }
        } catch (error) {
          console.warn('Failed to get thread from localStorage fallback:', error);
          resolve(null);
        }
      };
    });
  }

  async putThread(threadObject) {
    if (this.fallbackToLocalStorage) {
      try {
        let threads = [];
        const stored = localStorage.getItem('is:threads');
        if (stored) {
          threads = JSON.parse(stored);
        }
        
        const existingIndex = threads.findIndex(t => t.id === threadObject.id);
        if (existingIndex !== -1) {
          threads[existingIndex] = threadObject;
        } else {
          threads.push(threadObject);
        }
        
        localStorage.setItem('is:threads', JSON.stringify(threads));
      } catch (error) {
        console.warn('Failed to save thread to localStorage:', error);
      }
      return;
    }
    
    if (!this.db) {
      await this.openDb();
    }
    
    if (!this.db) {
      // Fallback to localStorage
      try {
        let threads = [];
        const stored = localStorage.getItem('is:threads');
        if (stored) {
          threads = JSON.parse(stored);
        }
        
        const existingIndex = threads.findIndex(t => t.id === threadObject.id);
        if (existingIndex !== -1) {
          threads[existingIndex] = threadObject;
        } else {
          threads.push(threadObject);
        }
        
        localStorage.setItem('is:threads', JSON.stringify(threads));
      } catch (error) {
        console.warn('Failed to save thread to localStorage:', error);
      }
      return;
    }
    
    return new Promise((resolve, reject) => {
      const transaction = this.db.transaction([this.storeName], 'readwrite');
      const store = transaction.objectStore(this.storeName);
      const request = store.put(threadObject);
      
      request.onsuccess = () => resolve();
      request.onerror = () => {
        console.warn('Failed to save thread to IndexedDB:', request.error);
        // Fallback to localStorage
        try {
          let threads = [];
          const stored = localStorage.getItem('is:threads');
          if (stored) {
            threads = JSON.parse(stored);
          }
          
          const existingIndex = threads.findIndex(t => t.id === threadObject.id);
          if (existingIndex !== -1) {
            threads[existingIndex] = threadObject;
          } else {
            threads.push(threadObject);
          }
          
          localStorage.setItem('is:threads', JSON.stringify(threads));
          resolve();
        } catch (error) {
          console.warn('Failed to save thread to localStorage fallback:', error);
          resolve();
        }
      };
    });
  }

  async deleteThread(id) {
    if (this.fallbackToLocalStorage) {
      try {
        let threads = [];
        const stored = localStorage.getItem('is:threads');
        if (stored) {
          threads = JSON.parse(stored);
        }
        
        const filteredThreads = threads.filter(t => t.id !== id);
        localStorage.setItem('is:threads', JSON.stringify(filteredThreads));
      } catch (error) {
        console.warn('Failed to delete thread from localStorage:', error);
      }
      return;
    }
    
    if (!this.db) {
      await this.openDb();
    }
    
    if (!this.db) {
      // Fallback to localStorage
      try {
        let threads = [];
        const stored = localStorage.getItem('is:threads');
        if (stored) {
          threads = JSON.parse(stored);
        }
        
        const filteredThreads = threads.filter(t => t.id !== id);
        localStorage.setItem('is:threads', JSON.stringify(filteredThreads));
      } catch (error) {
        console.warn('Failed to delete thread from localStorage:', error);
      }
      return;
    }
    
    return new Promise((resolve, reject) => {
      const transaction = this.db.transaction([this.storeName], 'readwrite');
      const store = transaction.objectStore(this.storeName);
      const request = store.delete(id);
      
      request.onsuccess = () => resolve();
      request.onerror = () => {
        console.warn('Failed to delete thread from IndexedDB:', request.error);
        // Fallback to localStorage
        try {
          let threads = [];
          const stored = localStorage.getItem('is:threads');
          if (stored) {
            threads = JSON.parse(stored);
          }
          
          const filteredThreads = threads.filter(t => t.id !== id);
          localStorage.setItem('is:threads', JSON.stringify(filteredThreads));
          resolve();
        } catch (error) {
          console.warn('Failed to delete thread from localStorage fallback:', error);
          resolve();
        }
      };
    });
  }
}

// Global instance
window.IndexedDBConversationsAdapter = new IndexedDBConversationsAdapter();