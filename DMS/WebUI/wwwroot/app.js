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
                const card = document.createElement('div');
                card.className = 'card mb-3'; // Bootstrap card class

                // Card body
                card.innerHTML = `
                    <div class="card-body">
                        <div class="row">
                            <!-- First Column for Title, Content, and OCR Text -->
                            <div class="col-md-8">
                                <h5 class="card-title">${doc.title}</h5>
                                ${doc.ocrText
                        ? `<p class="card-text"><strong>OCR Text:</strong> ${doc.ocrText}</p>`
                        : '<p class="card-text text-muted">OCR Text: Not available</p>'}
                            </div>

                            <!-- Second Column for Metadata and Buttons -->
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
                                        <small class="text-muted">
                                            Last updated at: ${doc.updatedAt ? formatDate(doc.updatedAt) : '-'}
                                        </small>
                                    </div>
                                </div>
                                <div class="row mt-auto">
                                    <div class="col text-right margin-top">
                                        <!-- Edit Button -->
                                        <!-- <button class="btn btn-info text-uppercase" onclick="openEditModal(${doc.id}, '${doc.title}', '${doc.content}')">Edit</button> -->
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
    const titleInput = document.getElementById('docTitle');
    const fileInput = document.getElementById('docFile');

    // Validate title input
    if (!titleInput || titleInput.value.trim() === '') {
        showAlert('Document Title is required.', 'danger');
        return;
    }

    // Validate file input
    if (!fileInput || !fileInput.files[0]) {
        showAlert('Please select a file to upload.', 'danger');
        return;
    }

    const title = titleInput.value.trim();
    const file = fileInput.files[0];

    try {
        // Step 1: Create the document
        const createdDocument = await createDocument(title);
        const documentId = createdDocument.id;

        // Step 2: Upload the file for the created document
        await uploadFile(documentId, file);

        titleInput.value = '';
        fileInput.value = '';
        await fetchDocuments();
        showAlert('Document added successfully!', 'success');
    } catch (error) {
        showAlert(`Error adding document: ${error.message}`, 'danger');
    }
}

async function createDocument(title) {
    const documentDto = { title };

    const response = await fetch(`${apiUrl}/document`, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
        },
        body: JSON.stringify(documentDto),
    });

    if (!response.ok) {
        const errorData = await response.json();
        console.error('Error creating document:', errorData);
        throw new Error(errorData.message || 'Failed to create document');
    }

    const createdDocument = await response.json();
    console.log('Created Document:', createdDocument); // Debug log
    return createdDocument;
}

async function uploadFile(documentId, file) {
    const formData = new FormData();
    formData.append('documentFile', file);

    const response = await fetch(`${apiUrl}/document/${documentId}/upload`, {
        method: 'PUT',
        body: formData,
    });

    if (!response.ok) {
        const errorData = await response.json();
        console.error('Error uploading file:', errorData);
        throw new Error(errorData.message || 'Failed to upload file');
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

window.onload = async () => {
    await fetchDocuments();
    document.getElementById('addDocumentButton').addEventListener('click', addDocument);
};
