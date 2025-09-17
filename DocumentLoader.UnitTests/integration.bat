curl -X POST https://localhost:63995/api/Documents/upload
curl -X GET https://localhost:63995/api/Documents/search?query=test
curl -X DELETE https://localhost:63995/api/Documents/delete?document_id=1
curl -X DELETE https://localhost:63995/api/Documents/delete?document_id=test
curl -X PUT https://localhost:63995/api/Documents/update
curl -X PUT https://localhost:63995/api/Documents/update?document_id=1
curl -X PUT https://localhost:63995/api/Documents/update -H "Content-Type: application/json" -d {"documentId": 1, "content": "test"}
pause