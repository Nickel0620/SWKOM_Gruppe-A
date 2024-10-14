// test for the REST API
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

// Fetch the documents from the API and display them in a list
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
            documentList.innerHTML = 'No documents found!';
        } else {
            documents.forEach(doc => {
                const card = document.createElement('div');
                card.className = 'card mb-3'; // Bootstrap card class

                // Card body
                card.innerHTML = `
                    <div class="card-body">
                        <div class="row">
                            <!-- First Column for Title and Content -->
                            <div class="col-md-8">
                                <h5 class="card-title">${doc.title}</h5>
                                <p class="card-text">${doc.content}</p>
                            </div>

                            <!-- Second Column for ID, Created, Last Updated, and Buttons -->
                            <div class="col-md-4 border-left">
                                <div class="row">
                                    <div class="col text-right">
                                        <small class="text-muted">ID: ${doc.id}</small>
                                    </div>
                                </div>
                                <div class="row">
                                    <div class="col text-right">
                                        <small class="text-muted">Created at: ${formatDate(doc.createdAt)}</small>
                                    </div>
                                </div>
                                <div class="row">
                                    <div class="col text-right">
                                        <small class="text-muted">Last updated at: ${formatDate(doc.updatedAt)}</small>
                                    </div>
                                </div>
                                <div class="row mt-auto">
                                    <div class="col text-right margin-top">
                                        <!-- Edit Button -->
                                        <button class="btn btn-info text-uppercase" onclick="openEditModal(${doc.id}, '${doc.title}', '${doc.content}')">Edit</button>
                                        <!-- Delete Button -->
                                        <button class="btn btn-danger text-uppercase" onclick="deleteDocument(${doc.id})">Delete</button>
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
`;
                documentList.appendChild(card);
            });
        }
    } catch (error) {
        console.error('Error fetching documents:', error);
        documentList.innerHTML = 'Failed to fetch documents: ' + error.message;
    }
}

// Helper function to format the date
function formatDate(dateString) {
    return new Date(dateString).toLocaleString('en-GB', {
        timeZone: 'CET',
        hour12: false,
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit'
    });
}


async function addDocument() {
    const title = document.getElementById('docTitle').value;
    const content = document.getElementById('docContent').value;

    console.log("Adding Document:", { title, content });

    try {
        const response = await fetch('http://localhost:8081/document', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ title, content })
        });

        if (!response.ok) {
            const errorData = await response.json();

            // Extract the validation messages from the error response
            const validationMessages = errorData.errors
                ? Object.values(errorData.errors).flat()  // Keep as an array for bulleted list
                : ['Failed to add document: ' + response.statusText];  // Convert to array

            // Display the validation errors using Bootstrap alert
            showAlert(validationMessages, 'danger');
            return;
        }

        // Clear input fields
        document.getElementById('docTitle').value = '';
        document.getElementById('docContent').value = '';

        // Refresh the document list
        await fetchDocuments();

        // Show success message
        showAlert('Document added successfully!', 'success');
    } catch (error) {
        console.error('Error adding document:', error);
        showAlert('Error adding document: ' + error.message, 'danger');
    }
}

async function deleteDocument(id) {

    try {
        const response = await fetch(`http://localhost:8081/document/${id}`, {
            method: 'DELETE'
        });

        if (!response.ok) {
            if (response.status === 404) {
                showAlert(`Document with ID ${id} not found.`, 'danger');
            } else {
                const errorResponse = await response.text();
                showAlert('Failed to delete document: ' + errorResponse, 'danger');
            }
            return;
        }

        // Refresh the document list
        await fetchDocuments();

        // Show success message
        showAlert('Document deleted successfully!', 'success'); 
    } catch (error) {
        console.error('Error deleting document:', error);
        showAlert('Error deleting document: ' + error.message, 'danger');
    }
}

// Function to open the edit modal and populate the fields
function openEditModal(id, title, content) {
    // Set the values in the modal
    document.getElementById('editDocTitle').value = title;
    document.getElementById('editDocContent').value = content;

    // Store the document ID to be edited in a global variable
    window.currentEditDocumentId = id;

    // Show the modal
    $('#editDocumentModal').modal('show');
}

// Save changes when the Save button is clicked
document.getElementById('saveChangesButton').addEventListener('click', async () => {
    const title = document.getElementById('editDocTitle').value;
    const content = document.getElementById('editDocContent').value;

    console.log("Updating Document:", { id: window.currentEditDocumentId, title, content });

    try {
        const response = await fetch(`http://localhost:8081/document/${window.currentEditDocumentId}`, {
            method: 'PUT',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ title, content })
        });

        if (!response.ok) {
            const errorData = await response.json();
            const validationMessages = errorData.errors
                ? Object.values(errorData.errors).flat()
                : ['Failed to update document: ' + response.statusText];

            // Display the validation errors using Bootstrap alert
            showAlert(validationMessages, 'danger');
            return;
        }

        // Refresh the document list
        await fetchDocuments();

        // Close the modal
        $('#editDocumentModal').modal('hide');

        // Show success message
        showAlert('Document updated successfully!', 'success');
    } catch (error) {
        console.error('Error updating document:', error);
        showAlert('Error updating document: ' + error.message, 'danger');
    }
});

// Function to display Bootstrap alerts
function showAlert(messages, type) {
    const alertContainer = document.getElementById('alertContainer');
    const alertDiv = document.createElement('div');
    alertDiv.className = `alert alert-${type} alert-dismissible fade show`;
    alertDiv.role = 'alert';

    // Check if messages is an array or a single message
    if (Array.isArray(messages)) {
        const ul = document.createElement('ul');
        messages.forEach(message => {
            const li = document.createElement('li');
            li.textContent = message;  // Create a list item for each message
            ul.appendChild(li);
        });
        alertDiv.appendChild(ul);  // Append the list to the alert
    } else {
        alertDiv.textContent = messages;  // If it's a single message, just set the text
    }

    alertDiv.innerHTML +=
        '<button type="button" class="close" data-dismiss="alert" aria-label="Close">' +
        '<span aria-hidden="true">&times;</span></button>';

    alertContainer.appendChild(alertDiv);


    setTimeout(() => {
        $(alertDiv).alert('close');  // jQuery to close the alert after 5 seconds
    }, 5000);
}

// Fetch the messages and documents when the page loads
window.onload = async () => {
    await fetchHelloWorld();
    await fetchDocuments();

    // Attach event listeners to buttons
    document.getElementById('addDocumentButton').addEventListener('click', addDocument);
    document.getElementById('deleteDocumentButton').addEventListener('click', deleteDocument);
};
