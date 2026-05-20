// MCP Unity Launcher — runs the server with the Unity project as CWD
const { spawn } = require('child_process');
const path = require('path');

const unityProjectDir = path.join(__dirname, 'Oasis');
const serverIndex = path.join(
  unityProjectDir,
  'Library', 'PackageCache',
  'com.gamelovers.mcp-unity@a32e47d4ec87',
  'Server~', 'build', 'index.js'
);

const server = spawn(process.execPath, [serverIndex], {
  cwd: unityProjectDir,
  stdio: 'inherit',
  env: { ...process.env, UNITY_PORT: '8090' }
});

server.on('exit', (code) => process.exit(code ?? 0));
server.on('error', (err) => {
  process.stderr.write('Launcher error: ' + err.message + '\n');
  process.exit(1);
});
