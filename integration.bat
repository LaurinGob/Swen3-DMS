@echo off
set "API_URL=http://localhost:5000/api/Documents"
set "TEST_FILE=test-document.pdf"

echo =======================================================
echo DMS INTEGRATION TEST: Use Case Document Upload
echo =======================================================

:: 1. Schritt: Upload
echo [STEP 1] Uploading file '%TEST_FILE%'...
curl -X POST "%API_URL%/upload" ^
     -H "Content-Type: multipart/form-data" ^
     -F "file=@%TEST_FILE%"
echo.
echo.

:: 2. Schritt: Verifizierung über Search
echo [STEP 2] Verifying upload via Search...
curl -X GET "%API_URL%/search?query=test" ^
     -H "accept: text/plain"
echo.
echo.

:: 3. Schritt: OCR Trigger (Critical Path zu RabbitMQ)
echo [STEP 3] Triggering Summary Generation (OCR/RabbitMQ Path)...
echo (Assuming ID 37 for demonstration - adjust if needed)
curl -X POST "%API_URL%/37/summaries" ^
     -H "accept: */*"
echo.
echo.
echo [STEP 4] Checking summary
curl -X GET "http://localhost:5000/api/Documents/37" -H "accept: application/json"

echo =======================================================
echo Integration Test finished.
echo =======================================================
pause