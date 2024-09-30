async function fetchHelloWorld() {
    const messageElement = document.getElementById('message');
    messageElement.textContent = 'Loading...';  // Initial loading message
    try {
        const response = await fetch('/api/HelloWorld');
        if (!response.ok) {
            throw new Error('Network response was not ok');
        }
        const message = await response.text();
        messageElement.textContent = message;  // Display the message from API
    } catch (error) {
        console.error('Error fetching message:', error);
        messageElement.textContent = 'Failed to fetch message: ' + error.message;
    }
}


// Fetch the message when the page loads
window.onload = fetchHelloWorld;
