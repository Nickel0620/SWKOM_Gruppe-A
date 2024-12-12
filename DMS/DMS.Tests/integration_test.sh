#!/bin/bash

API_URL="http://localhost:8080"
MINIO_URL="http://localhost:9000"
RABBITMQ_URL="http://localhost:15672"
MINIO_ACCESS_KEY="minioadmin"
MINIO_SECRET_KEY="minioadmin"

# File to upload
TEST_FILE="Hello_World.pdf"

# Helper function for assertions
assert_response() {
    local response="$1"
    local expected="$2"
    if [[ "$response" != *"$expected"* ]]; then
        echo "Test failed: Expected '$expected' in response. Got: $response"
        exit 1
    fi
    echo "Test passed."
}

# Step 1: Add a document
echo "Uploading file to MinIO..."
FILE_KEY=$(curl -s -X POST "$API_URL/api/file/upload" \
    -H "Content-Type: multipart/form-data" \
    -F "file=@$TEST_FILE" | jq -r '.fileName')

if [[ -z "$FILE_KEY" ]]; then
    echo "Error: File upload failed. No file key returned."
    exit 1
fi

echo "File uploaded with key: $FILE_KEY"

echo "Adding document..."
ADD_RESPONSE=$(curl -s -X POST "$API_URL/document" \
    -H "Content-Type: application/json" \
    -d '{"title": "Test Document", "filePath": "'"$FILE_KEY"'"}')

DOC_ID=$(echo "$ADD_RESPONSE" | jq -r '.id')

if [[ -z "$DOC_ID" ]]; then
    echo "Error: Document creation failed. No document ID returned."
    exit 1
fi

echo "Added document with ID: $DOC_ID"
assert_response "$ADD_RESPONSE" "$DOC_ID"

# Wait for OCR processing and Elasticsearch indexing
echo "Waiting for RabbitMQ and OcrWorker..."
MAX_RETRIES=10
SLEEP_INTERVAL=2
RETRY_COUNT=0

while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
    OCR_TEXT=$(curl -s "$API_URL/document/$DOC_ID" | jq -r '.ocrText')
    if [[ "$OCR_TEXT" != "null" && -n "$OCR_TEXT" ]]; then
        echo "OCR text populated: $OCR_TEXT"
        break
    fi
    echo "Waiting for OCRText to be populated and put in ElasticSearch..."
    sleep $SLEEP_INTERVAL
    RETRY_COUNT=$((RETRY_COUNT + 1))
done

if [[ $RETRY_COUNT -eq $MAX_RETRIES ]]; then
    echo "Test failed: OCR text not populated within expected time."
    exit 1
fi

# Step 2: Get documents
echo "Fetching documents..."
GET_RESPONSE=$(curl -s "$API_URL/document")
assert_response "$GET_RESPONSE" "$DOC_ID"

# Step 3: Search documents (normal)
echo "Performing normal search with the term \"World\"..."
SEARCH_RESPONSE=$(curl -s -X POST "$API_URL/document/search/querystring" \
    -H "Content-Type: application/json" \
    -d '"World"')

# Filter results by filePath
MATCHED_DOC=$(echo "$SEARCH_RESPONSE" | jq ".[] | select(.filePath == \"$FILE_KEY\")")

if [[ -z "$MATCHED_DOC" ]]; then
    echo "Test failed: No matching document found in search results."
    echo "Response: $SEARCH_RESPONSE"
    exit 1
fi
echo "Test passed: Matching document found."

# Step 4: Search documents (fuzzy)
echo "Performing fuzzy search with the term \"Wrold\"..."
FUZZY_SEARCH_RESPONSE=$(curl -s -X POST "$API_URL/document/search/fuzzy" \
    -H "Content-Type: application/json" \
    -d '"Wrold"')

# Filter results by filePath
MATCHED_FUZZY_DOC=$(echo "$FUZZY_SEARCH_RESPONSE" | jq ".[] | select(.filePath == \"$FILE_KEY\")")

if [[ -z "$MATCHED_FUZZY_DOC" ]]; then
    echo "Test failed: No matching document found in fuzzy search results."
    echo "Response: $FUZZY_SEARCH_RESPONSE"
    exit 1
fi
echo "Test passed: Matching document found in fuzzy search."

# Step 5: Check MinIO for file
echo "Checking file in MinIO..."
MINIO_RESPONSE=$(curl -s -u "$MINIO_ACCESS_KEY:$MINIO_SECRET_KEY" \
    "$MINIO_URL/minio/data/uploads/$FILE_KEY")
if [[ -z "$MINIO_RESPONSE" ]]; then
    echo "Error: File not found in MinIO."
    exit 1
fi
echo "File verified in MinIO."

# Step 6: Download the document
echo "Downloading document with ID: $DOC_ID..."
DOWNLOAD_FILE="downloaded_$TEST_FILE"

curl -s -o "$DOWNLOAD_FILE" -X GET "$API_URL/document/download/$DOC_ID"

# Check if the downloaded file exists and is not empty
if [[ ! -s "$DOWNLOAD_FILE" ]]; then
    echo "Error: Downloaded file is empty or missing."
    exit 1
fi

echo "File downloaded successfully."

# Optional: Compare the original and downloaded file contents
if cmp -s "$TEST_FILE" "$DOWNLOAD_FILE"; then
    echo "File contents match the original file."
else
    echo "Error: Downloaded file content does not match the original file."
    exit 1
fi

# Cleanup: Remove the downloaded file
rm "$DOWNLOAD_FILE"

# Step 7: Delete the document
echo "Deleting document with ID: $DOC_ID..."
DELETE_RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" -X DELETE "$API_URL/document/$DOC_ID")

if [[ "$DELETE_RESPONSE" -ne 204 ]]; then
    echo "Error: Unexpected response code from DELETE endpoint. Expected 204, got: $DELETE_RESPONSE"
    exit 1
fi

echo "Document deleted successfully."

# Step 8: Verify the document no longer exists in DAL
echo "Verifying document does not exist in DAL..."
GET_DELETED_DOCUMENT=$(curl -s -X GET "$API_URL/document/$DOC_ID")

if [[ "$GET_DELETED_DOCUMENT" != *"Document not found"* ]]; then
    echo "Error: Document still exists in DAL after deletion."
    echo "Response: $GET_DELETED_DOCUMENT"
    exit 1
fi
echo "Verified: Document does not exist in DAL."

# Step 9: Verify the file no longer exists in MinIO
echo "Verifying file does not exist in MinIO..."
MINIO_DELETED_RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" -u "$MINIO_ACCESS_KEY:$MINIO_SECRET_KEY" \
    "$MINIO_URL/minio/data/uploads/$FILE_KEY")

if [[ "$MINIO_DELETED_RESPONSE" == "404" ]]; then
    echo "Verified: File does not exist in MinIO."
elif [[ "$MINIO_DELETED_RESPONSE" == "400" ]]; then
    echo "Verified: File does not exist in MinIO."
else
    echo "Error: Unexpected response from MinIO. HTTP Status: $MINIO_DELETED_RESPONSE"
    exit 1
fi


echo "All tests passed!"