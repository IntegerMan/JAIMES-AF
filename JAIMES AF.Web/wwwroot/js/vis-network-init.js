// vis-network JS interop for Blazor
// Provides force-directed graph visualization with radial layout

window.visNetworkInterop = {
    networks: {},

    /**
     * Creates a vis-network graph in the specified container
     * @param {string} containerId - The ID of the container element
     * @param {object} graphData - Object with nodes and edges arrays
     * @param {object} dotNetRef - .NET reference for callbacks
     * @param {string} currentLocationId - The ID of the current/center location (optional)
     */
    createNetwork: function (containerId, graphData, dotNetRef, currentLocationId) {
        const container = document.getElementById(containerId);
        if (!container) {
            console.error('vis-network: Container not found:', containerId);
            return;
        }

        // Destroy existing network if present
        if (this.networks[containerId]) {
            this.networks[containerId].destroy();
            delete this.networks[containerId];
        }

        // Warning theme colors
        const warningColor = '#FF9800';
        const warningDark = '#F57C00';
        const warningLight = '#FFE0B2';
        const currentNodeColor = '#FFD60A';

        // Transform nodes for vis-network
        const nodes = new vis.DataSet(graphData.nodes.map(node => ({
            id: node.id,
            label: node.label,
            title: node.tooltip || node.label,
            color: {
                background: node.id === currentLocationId ? currentNodeColor : warningLight,
                border: node.id === currentLocationId ? warningDark : warningColor,
                highlight: {
                    background: currentNodeColor,
                    border: warningDark
                },
                hover: {
                    background: '#FFECB3',
                    border: warningDark
                }
            },
            borderWidth: node.id === currentLocationId ? 3 : 2,
            font: {
                color: '#333',
                size: 14,
                face: 'Roboto, sans-serif',
                bold: node.id === currentLocationId
            },
            shape: 'dot',
            size: node.id === currentLocationId ? 25 : 18
        })));

        // Transform edges for vis-network
        const edges = new vis.DataSet(graphData.edges.map(edge => ({
            from: edge.from,
            to: edge.to,
            label: edge.label || '',
            title: edge.tooltip || edge.label || '',
            color: {
                color: '#BDBDBD',
                highlight: warningColor,
                hover: warningColor
            },
            font: {
                color: '#666',
                size: 11,
                strokeWidth: 0,
                align: 'middle'
            },
            smooth: {
                type: 'continuous',
                roundness: 0.3
            }
        })));

        const data = { nodes: nodes, edges: edges };

        const options = {
            physics: {
                enabled: true,
                solver: 'forceAtlas2Based',
                forceAtlas2Based: {
                    gravitationalConstant: -50,
                    centralGravity: 0.01,
                    springLength: 120,
                    springConstant: 0.08,
                    damping: 0.4
                },
                stabilization: {
                    enabled: true,
                    iterations: 200,
                    fit: true
                }
            },
            layout: {
                improvedLayout: true,
                randomSeed: 42
            },
            interaction: {
                hover: true,
                tooltipDelay: 200,
                zoomView: true,
                dragView: true
            },
            nodes: {
                shadow: {
                    enabled: true,
                    color: 'rgba(0,0,0,0.1)',
                    size: 8,
                    x: 2,
                    y: 2
                }
            },
            edges: {
                width: 2,
                selectionWidth: 3
            }
        };

        const network = new vis.Network(container, data, options);
        this.networks[containerId] = network;

        // Handle node clicks - navigate to location
        network.on('click', function (params) {
            if (params.nodes.length > 0) {
                const nodeId = params.nodes[0];
                if (dotNetRef) {
                    dotNetRef.invokeMethodAsync('OnNodeClicked', nodeId.toString());
                }
            }
        });

        // Handle double-click for zoom
        network.on('doubleClick', function (params) {
            if (params.nodes.length > 0) {
                network.focus(params.nodes[0], {
                    scale: 1.5,
                    animation: {
                        duration: 500,
                        easingFunction: 'easeInOutQuad'
                    }
                });
            }
        });

        // Fit the network to the container once stabilized
        network.once('stabilizationIterationsDone', function () {
            network.fit({
                animation: {
                    duration: 300,
                    easingFunction: 'easeOutQuad'
                }
            });
        });

        return true;
    },

    /**
     * Updates the graph data without recreating the entire network
     * @param {string} containerId - The ID of the container element
     * @param {object} graphData - Object with nodes and edges arrays
     * @param {string} currentLocationId - The ID of the current/center location
     */
    updateNetwork: function (containerId, graphData, currentLocationId) {
        const network = this.networks[containerId];
        if (!network) {
            console.error('vis-network: Network not found for container:', containerId);
            return false;
        }

        // Update would require more complex logic - for now, just recreate
        return false;
    },

    /**
     * Destroys the network and cleans up resources
     * @param {string} containerId - The ID of the container element
     */
    destroyNetwork: function (containerId) {
        if (this.networks[containerId]) {
            this.networks[containerId].destroy();
            delete this.networks[containerId];
            return true;
        }
        return false;
    },

    /**
     * Fits the network view to show all nodes
     * @param {string} containerId - The ID of the container element
     */
    fitNetwork: function (containerId) {
        const network = this.networks[containerId];
        if (network) {
            network.fit({
                animation: {
                    duration: 500,
                    easingFunction: 'easeInOutQuad'
                }
            });
            return true;
        }
        return false;
    },

    /**
     * Focuses on a specific node
     * @param {string} containerId - The ID of the container element
     * @param {string} nodeId - The ID of the node to focus on
     */
    focusNode: function (containerId, nodeId) {
        const network = this.networks[containerId];
        if (network) {
            network.focus(nodeId, {
                scale: 1.2,
                animation: {
                    duration: 500,
                    easingFunction: 'easeInOutQuad'
                }
            });
            return true;
        }
        return false;
    }
};
