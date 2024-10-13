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

async function fetchDocuments() {
    const documentList = document.getElementById('documentList');
    documentList.innerHTML = 'Loading...';  // Initial loading message
    try {
        const response = await fetch('http://localhost:8081/document');  // Call Document API
        if (!response.ok) {
            throw new Error('Network response was not ok');
        }
        const documents = await response.json();
        documentList.innerHTML = '';  // Clear loading message

        if (documents.length === 0) {
            documentList.innerHTML = '<li>No documents found.</li>';
        } else {
            documents.forEach(doc => {
                const listItem = document.createElement('li');

                // Format the created and updated dates for CET in European format
                const createdAt = new Date(doc.createdAt).toLocaleString('en-GB', {
                    timeZone: 'CET',
                    hour12: false,  // 24-hour clock
                    day: '2-digit',
                    month: '2-digit',
                    year: 'numeric',
                    hour: '2-digit',
                    minute: '2-digit'
                });

                const updatedAt = new Date(doc.updatedAt).toLocaleString('en-GB', {
                    timeZone: 'CET',
                    hour12: false,  // 24-hour clock
                    day: '2-digit',
                    month: '2-digit',
                    year: 'numeric',
                    hour: '2-digit',
                    minute: '2-digit'
                });

                // Set the list item text
                listItem.textContent = `${doc.title} (ID: ${doc.id}) - Created At: ${createdAt} - Updated At: ${updatedAt}`;
                documentList.appendChild(listItem);
            });
        }
    } catch (error) {
        console.error('Error fetching documents:', error);
        documentList.innerHTML = 'Failed to fetch documents: ' + error.message;
    }
}



async function addDocument() {
    const title = document.getElementById('docTitle').value;
    const content = document.getElementById('docContent').value;

    console.log("Adding Document:", { title, content }); // Add this line to log input

    if (!title || !content) {
        alert('Please fill in all fields.');
        return;
    }

    try {
        const response = await fetch('http://localhost:8081/document', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ title, content })
        });

        if (!response.ok) {
            throw new Error('Failed to add document: ' + response.statusText);
        }

        // Clear input fields
        document.getElementById('docTitle').value = '';
        document.getElementById('docContent').value = '';

        // Refresh the document list
        await fetchDocuments();
    } catch (error) {
        console.error('Error adding document:', error);
        alert('Error adding document: ' + error.message);
    }
}


async function deleteDocument() {
    const id = document.getElementById('docId').value;

    if (!id) {
        alert('Please enter a document ID.');
        return;
    }

    try {
        const response = await fetch(`http://localhost:8081/document/${id}`, {
            method: 'DELETE'
        });

        if (!response.ok) {
            throw new Error('Failed to delete document: ' + response.statusText);
        }

        // Clear the input field
        document.getElementById('docId').value = '';

        // Refresh the document list
        await fetchDocuments();
    } catch (error) {
        console.error('Error deleting document:', error);
        alert('Error deleting document: ' + error.message);
    }
}

// Fetch the messages and documents when the page loads
window.onload = async () => {
    await fetchHelloWorld();
    await fetchDocuments();

    // Attach event listeners to buttons
    document.getElementById('addDocumentButton').addEventListener('click', addDocument);
    document.getElementById('deleteDocumentButton').addEventListener('click', deleteDocument);
};
