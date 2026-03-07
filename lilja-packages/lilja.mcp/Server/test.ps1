$json_init = '{"jsonrpc": "2.0", "id": 0, "method": "initialize", "params": { "protocolVersion": "2024-11-05", "capabilities": {}, "clientInfo": { "name": "cursor", "version": "1.0.0" } }}'
Write-Host "--- TEST 0: initialize ---"
$json_init | cargo run -q

$json1 = '{"jsonrpc": "2.0", "id": 1, "method": "tools/list"}'
Write-Host "`n--- TEST 1: tools/list ---"
$json1 | cargo run -q

$json2 = '{"jsonrpc": "2.0", "id": 2, "method": "tools/call", "params": {"name": "add", "arguments": {"a": 3.5, "b": 4.2}}}'
Write-Host "`n--- TEST 2: tools/call (add) ---"
$json2 | cargo run -q

$json3 = '{"jsonrpc": "2.0", "id": 3, "method": "tools/call", "params": {"name": "divide", "arguments": {"a": 5, "b": 0}}}'
Write-Host "`n--- TEST 3: tools/call (divide by zero) ---"
$json3 | cargo run -q

$json4 = '{"jsonrpc": "2.0", "id": 4, "method": "tools/call", "params": {"name": "multiply", "arguments": {"a": 10, "b": -2}}}'
Write-Host "`n--- TEST 4: tools/call (multiply) ---"
$json4 | cargo run -q

$json5 = '{"jsonrpc": "2.0", "id": 5, "method": "tools/call", "params": {"name": "subtract", "arguments": {"a": 100, "b": 30}}}'
Write-Host "`n--- TEST 5: tools/call (subtract) ---"
$json5 | cargo run -q
