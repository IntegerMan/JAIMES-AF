window.initializeMermaid = async (elementId, mermaidDefinition) => {
    const element = document.getElementById(elementId);
    if (!element) {
        console.error(`Element with id '${elementId}' not found`);
        return;
    }

    // Initialize Mermaid if not already initialized
    if (typeof mermaid === 'undefined') {
        console.error('Mermaid.js is not loaded. Please include the Mermaid.js script.');
        element.innerHTML = '<p style="color: red;">Mermaid.js library is not loaded.</p>';
        return;
    }

    // Initialize Mermaid with default config (only once)
    if (!window.mermaidInitialized) {
        mermaid.initialize({ 
            startOnLoad: false,
            theme: 'default',
            flowchart: {
                useMaxWidth: true,
                htmlLabels: true,
                curve: 'basis'
            }
        });
        window.mermaidInitialized = true;
    }

    // Clear any existing content
    element.innerHTML = '';

    // Create a unique ID for this diagram
    const diagramId = `mermaid-${elementId}-${Date.now()}`;
    
    // Set up the element for Mermaid rendering
    element.className = 'mermaid';
    element.id = diagramId;
    element.textContent = mermaidDefinition.trim();

    // Render the diagram
    try {
        // Wait for Mermaid to be ready
        if (mermaid.run) {
            // Mermaid 10.x API
            await mermaid.run({
                nodes: [element]
            });
        } else if (mermaid.contentLoaded) {
            // Alternative API
            mermaid.contentLoaded();
        } else {
            // Fallback: try to render directly
            const { svg } = await mermaid.render(diagramId, mermaidDefinition);
            element.innerHTML = svg;
            element.className = '';
        }
    } catch (error) {
        console.error('Error rendering Mermaid diagram:', error);
        element.innerHTML = '<p style="color: red; padding: 1rem;">Error rendering diagram. Please check the console for details.</p>';
        element.className = '';
    }
};

