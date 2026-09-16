import { spawn } from "node:child_process";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const root = dirname(fileURLToPath(import.meta.url));
const nextBin = join(root, "..", "node_modules", "next", "dist", "bin", "next");
const nodeOptions = [process.env.NODE_OPTIONS, "--use-system-ca"].filter(Boolean).join(" ");

const child = spawn(process.execPath, [nextBin, "dev", "--port", "3000"], {
  stdio: "inherit",
  cwd: join(root, ".."),
  env: { ...process.env, NODE_OPTIONS: nodeOptions }
});

child.on("exit", (code) => {
  process.exit(code ?? 1);
});
