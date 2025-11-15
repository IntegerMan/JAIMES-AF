window.scrollToBottom = (elementId) => {
    const element = document.getElementById(elementId);
    if (element) {
        // Use multiple attempts with delays to ensure scrolling works
        const scroll = () => {
            element.scrollTop = element.scrollHeight;
        };
        
        // Try immediately
        scroll();
        
        // Try after a short delay
        setTimeout(scroll, 10);
        
        // Try after requestAnimationFrame
        requestAnimationFrame(() => {
            scroll();
            // One more attempt after a brief delay
            setTimeout(scroll, 50);
        });
    }
};

