const apiUrl = 'http://localhost:8080';

// Fetch the documents from the API and display them in a list
async function fetchDocuments() {
    const documentList = document.getElementById('documentList');
    documentList.innerHTML = 'Loading...'; // Initial loading message

    try {
        const response = await fetch(`${apiUrl}/document`); // Call Document API
        if (!response.ok) {
            throw new Error('Network response was not ok');
        }
        const documents = await response.json();
        documentList.innerHTML = ''; // Clear loading message

        if (documents.length === 0) {
            documentList.innerHTML = 'No documents found!';
        } else {
            documents.forEach(doc => {
                documentList.innerHTML += createDocumentCard(doc); // Use the reusable function
            });
        }
    } catch (error) {
        console.error('Error fetching documents:', error);
        documentList.innerHTML = 'Failed to fetch documents: ' + error.message;
    }
}

// Function to show the OCR Text in the modal
function showOcrText(ocrText) {
    const ocrTextContent = document.getElementById('ocrTextContent');
    ocrTextContent.textContent = ocrText; // Set OCR text content
    $('#ocrTextModal').modal('show'); // Show the modal using Bootstrap
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
    const titleInput = document.getElementById('docTitle');
    const fileInput = document.getElementById('docFile');

    if (!titleInput || titleInput.value.trim() === '') {
        showAlert('Document Title is required.', 'danger');
        return;
    }

    if (!fileInput || !fileInput.files[0]) {
        showAlert('Please select a file to upload.', 'danger');
        return;
    }

    const title = titleInput.value.trim();
    const file = fileInput.files[0];

    try {
        // Step 1: Upload the file to MinIO using FileController
        const fileKey = await uploadFileToMinio(file);

        // Step 2: Send the document metadata along with the FileKey
        await createDocument(title, fileKey);

        titleInput.value = '';
        fileInput.value = '';
        await fetchDocuments();
        showAlert('Document added successfully!', 'success');
    } catch (error) {
        showAlert(`Error adding document: ${error.message}`, 'danger');
    }
}

async function uploadFileToMinio(file) {
    const formData = new FormData();
    formData.append('file', file);

    const response = await fetch(`${apiUrl}/api/file/upload`, {
        method: 'POST',
        body: formData,
    });

    if (!response.ok) {
        const errorData = await response.json();
        console.error('Error uploading file to MinIO:', errorData);
        throw new Error(errorData.message || 'Failed to upload file');
    }

    const { fileName } = await response.json(); // Get the file key from the response
    return fileName;
}

async function createDocument(title, fileKey) {
    const response = await fetch(`${apiUrl}/document`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify({ title, filePath: fileKey }), // Pass the FileKey in the payload
    });

    if (!response.ok) {
        const errorData = await response.json();
        console.error('Error creating document:', errorData);
        throw new Error(errorData.message || 'Failed to create document');
    }
}


async function deleteDocument(id) {
    try {
        const response = await fetch(`${apiUrl}/document/${id}`, {
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

        await fetchDocuments();
        showAlert('Document deleted successfully!', 'success');
    } catch (error) {
        console.error('Error deleting document:', error);
        showAlert('Error deleting document: ' + error.message, 'danger');
    }
}

//function openEditModal(id, title, content) {
//    document.getElementById('editDocTitle').value = title;
//    document.getElementById('editDocContent').value = content;
//    window.currentEditDocumentId = id;
//    $('#editDocumentModal').modal('show');
//}

//document.getElementById('saveChangesButton').addEventListener('click', async () => {
//    const title = document.getElementById('editDocTitle').value;
//    const content = document.getElementById('editDocContent').value;

//    try {
//        const response = await fetch(`http://localhost:8080/document/${window.currentEditDocumentId}`, {
//            method: 'PUT',
//            headers: {
//                'Content-Type': 'application/json'
//            },
//            body: JSON.stringify({ title, content })
//        });

//        if (!response.ok) {
//            const errorData = await response.json();
//            const validationMessages = errorData.errors
//                ? Object.values(errorData.errors).flat()
//                : [`Failed to update document: ${response.statusText}`];
//            showAlert(validationMessages, 'danger');
//            return;
//        }

//        await fetchDocuments();
//        $('#editDocumentModal').modal('hide');
//        showAlert('Document updated successfully!', 'success');
//    } catch (error) {
//        console.error('Error updating document:', error);
//        showAlert('Error updating document: ' + error.message, 'danger');
//    }
//});

// Search and display filtered documents
async function searchDocuments() {
    const searchBox = document.getElementById('searchBox');
    const searchTerm = searchBox.value.trim();

    if (!searchTerm) {
        showAlert('Please enter a search term.', 'warning');
        return;
    }

    const documentList = document.getElementById('documentList');
    documentList.innerHTML = 'Loading...';

    try {
        // Perform normal search
        let response = await fetch(`${apiUrl}/document/search/querystring`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(searchTerm),
        });

        let documents = await response.json();

        // If no results, fallback to fuzzy search
        if (documents.length === 0) {
            response = await fetch(`${apiUrl}/document/search/fuzzy`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(searchTerm),
            });

            if (!response.ok) {
                throw new Error('Fuzzy search failed. Please try again.');
            }

            documents = await response.json();
        }

        documentList.innerHTML = '';

        if (documents.length === 0) {
            documentList.innerHTML = 'No matching documents found.';
        } else {
            documents.forEach(doc => {
                documentList.innerHTML += createDocumentCard(doc);
            });
        }
    } catch (error) {
        console.error('Error during search:', error);
        showAlert(`Error searching documents: ${error.message}`, 'danger');
    }
}

function showAlert(messages, type) {
    const alertContainer = document.getElementById('alertContainer');
    const alertDiv = document.createElement('div');
    alertDiv.className = `alert alert-${type} alert-dismissible fade show`;
    alertDiv.role = 'alert';

    if (Array.isArray(messages)) {
        const ul = document.createElement('ul');
        messages.forEach(message => {
            const li = document.createElement('li');
            li.textContent = message;
            ul.appendChild(li);
        });
        alertDiv.appendChild(ul);
    } else {
        alertDiv.textContent = messages;
    }

    alertDiv.innerHTML += `
        <button type="button" class="close" data-dismiss="alert" aria-label="Close">
            <span aria-hidden="true">&times;</span>
        </button>
    `;

    alertContainer.appendChild(alertDiv);

    setTimeout(() => {
        $(alertDiv).alert('close');
    }, 5000);
}

// Function to create a card for a document
function createDocumentCard(doc) {
    return `
        <div class="card mb-3">
            <div class="card-body pb-2">
                <div class="row align-items-center">
                    <!-- Title -->
                    <div class="col-md-8">
                        <h5 class="card-title">${doc.title}</h5>
                    </div>

                    <!-- Buttons -->
                    <div class="col-md-4 text-right">
                        <button class="btn btn-light-gray px-2 py-1 mr-2" onclick="downloadDocument(${doc.id})">
                            <i class="fas fa-download"></i>
                        </button>
                        <button class="btn btn-deep-red px-2 py-1" onclick="deleteDocument(${doc.id})">
                            <i class="fas fa-trash-alt"></i>
                        </button>
                    </div>
                </div>

                <!-- Horizontal Divider -->
                <hr class="mt-4 mb-2" />

                <!-- Metadata Row -->
                <div class="row mb-0">
                    <!-- ID -->
                    <div class="col text-left">
                        <small class="text-muted">ID: ${doc.id}</small>
                    </div>

                    <!-- Created At -->
                    <div class="col text-right">
                        <small class="text-muted">Created at: ${formatDate(doc.createdAt)}</small>
                    </div>
                </div>
            </div>
        </div>
    `;
}


window.onload = async () => {
    await fetchDocuments();
    document.getElementById('addDocumentButton').addEventListener('click', addDocument);

    document.getElementById('searchButton').addEventListener('click', searchDocuments);

    // trigger search on Enter key press in the search box
    document.getElementById('searchBox').addEventListener('keypress', (event) => {
        if (event.key === 'Enter') {
            event.preventDefault();
            searchDocuments();
        }
    });

    document.getElementById("refreshButton").addEventListener("click", function () {
        document.getElementById("searchBox").value = "";
        const documentList = document.getElementById("documentList");
        documentList.innerHTML = '';
        fetchDocuments();
    });

};
