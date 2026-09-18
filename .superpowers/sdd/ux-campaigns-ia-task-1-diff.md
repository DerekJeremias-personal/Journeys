# Task 1 review package (working tree; no commit)

**Base:** HEAD (uncommitted Task 1 files only)
**Head:** working tree

## git diff --stat

 Journeys/Journeys.UX/package-lock.json             | 2936 ++++++++++++++++++--  Journeys/Journeys.UX/package.json                  |    9 +  Journeys/Journeys.UX/src/app/globals.css           |   72 +-  Journeys/Journeys.UX/src/app/layout.tsx            |    5 +-  Journeys/Journeys.UX/src/app/loyalty/layout.tsx    |    4 +-  .../Journeys.UX/src/components/loyalty-nav.tsx     |   29 +-  Journeys/Journeys.UX/src/lib/api-types.ts          |   11 +  .../Journeys.UX/src/lib/journeys-fetch.test.ts     |   27 +-  Journeys/Journeys.UX/src/lib/journeys-fetch.ts     |    7 +  .../Journeys.UX/src/lib/map-loyalty-path.test.ts   |   47 +-  Journeys/Journeys.UX/src/lib/map-loyalty-path.ts   |   42 +  .../Journeys.UX/src/services/loyalty/actions.ts    |  125 +-  12 files changed, 3061 insertions(+), 253 deletions(-)

## untracked

Journeys/Journeys.UX/postcss.config.mjs
Journeys/Journeys.UX/src/components/providers.tsx
Journeys/Journeys.UX/src/components/ui/alert-dialog.tsx
Journeys/Journeys.UX/src/components/ui/alert.tsx
Journeys/Journeys.UX/src/components/ui/badge.tsx
Journeys/Journeys.UX/src/components/ui/button.tsx
Journeys/Journeys.UX/src/components/ui/card.tsx
Journeys/Journeys.UX/src/components/ui/dropdown-menu.tsx
Journeys/Journeys.UX/src/components/ui/skeleton.tsx
Journeys/Journeys.UX/src/lib/utils.ts

## Diff

diff --git a/Journeys/Journeys.UX/package-lock.json b/Journeys/Journeys.UX/package-lock.json
index 0b223d7..f59ec8b 100644
--- a/Journeys/Journeys.UX/package-lock.json
+++ b/Journeys/Journeys.UX/package-lock.json
@@ -1,34 +1,55 @@
 {
   "name": "journeys-ux",
   "version": "0.0.0",
   "lockfileVersion": 3,
   "requires": true,
   "packages": {
     "": {
       "name": "journeys-ux",
       "version": "0.0.0",
       "dependencies": {
+        "@tailwindcss/postcss": "^4.3.3",
+        "@tanstack/react-query": "^5.103.1",
+        "class-variance-authority": "^0.7.1",
+        "clsx": "^2.1.1",
+        "lucide-react": "^1.47.0",
         "next": "^16.0.7",
         "next-auth": "5.0.0-beta.30",
+        "radix-ui": "^1.6.7",
         "react": "^19.0.0",
         "react-dom": "^19.0.0",
+        "react-hot-toast": "^2.6.1",
+        "tailwind-merge": "^3.7.0",
+        "tailwindcss": "^4.3.3",
         "zod": "^4.0.0"
       },
       "devDependencies": {
         "@types/node": "^22.0.0",
         "@types/react": "^19.0.0",
         "@types/react-dom": "^19.0.0",
         "typescript": "^5.6.0",
         "vitest": "^3.0.0"
       }
     },
+    "node_modules/@alloc/quick-lru": {
+      "version": "5.3.0",
+      "resolved": "https://registry.npmjs.org/@alloc/quick-lru/-/quick-lru-5.3.0.tgz",
+      "integrity": "sha512-U4+70Pc5ZS9osnCBCE5Jha/ciHM+Yp+CNMNC/7HvYbNRk1Ldd+f7qO65W5qfhu/TCv+/ozljlXXe9Nj8419DMA==",
+      "license": "MIT",
+      "engines": {
+        "node": ">=10"
+      },
+      "funding": {
+        "url": "https://github.com/sponsors/sindresorhus"
+      }
+    },
     "node_modules/@auth/core": {
       "version": "0.41.0",
       "resolved": "https://registry.npmjs.org/@auth/core/-/core-0.41.0.tgz",
       "integrity": "sha512-Wd7mHPQ/8zy6Qj7f4T46vg3aoor8fskJm6g2Zyj064oQ3+p0xNZXAV60ww0hY+MbTesfu29kK14Zk5d5JTazXQ==",
       "license": "ISC",
       "dependencies": {
         "@panva/hkdf": "^1.2.1",
         "jose": "^6.0.6",
         "oauth4webapi": "^3.3.0",
         "preact": "10.24.3",
@@ -496,20 +517,58 @@
       "dev": true,
       "license": "MIT",
       "optional": true,
       "os": [
         "win32"
       ],
       "engines": {
         "node": ">=18"
       }
     },
+    "node_modules/@floating-ui/core": {
+      "version": "1.8.0",
+      "resolved": "https://registry.npmjs.org/@floating-ui/core/-/core-1.8.0.tgz",
+      "integrity": "sha512-0CIZ5itps/8x7BG8dEIhs53BvCUH2PCoogtakwRTut+Arm58sJooJ0AuZhLw2HJYIR5cMLNPBSS728sPho2khQ==",
+      "license": "MIT",
+      "dependencies": {
+        "@floating-ui/utils": "^0.2.12"
+      }
+    },
+    "node_modules/@floating-ui/dom": {
+      "version": "1.8.0",
+      "resolved": "https://registry.npmjs.org/@floating-ui/dom/-/dom-1.8.0.tgz",
+      "integrity": "sha512-yXSrzeHZBTZadLOlfyhCkJHNeLJnHRnRInwdZ40L7ZiaAtrBwoYlsDrX3v5zB1Utk7CLfzcOVnVVWoXEky7Ceg==",
+      "license": "MIT",
+      "dependencies": {
+        "@floating-ui/core": "^1.8.0",
+        "@floating-ui/utils": "^0.2.12"
+      }
+    },
+    "node_modules/@floating-ui/react-dom": {
+      "version": "2.1.9",
+      "resolved": "https://registry.npmjs.org/@floating-ui/react-dom/-/react-dom-2.1.9.tgz",
+      "integrity": "sha512-JDjEFGCpImxDCA7JJKviA0M9+RtmJdj0m/NVU5IMgBK+AmZouAQQ7/+2GLH0GXXY0YMw9oXPB8hKdbPYg5QLYg==",
+      "license": "MIT",
+      "dependencies": {
+        "@floating-ui/dom": "^1.8.0"
+      },
+      "peerDependencies": {
+        "react": ">=16.8.0",
+        "react-dom": ">=16.8.0"
+      }
+    },
+    "node_modules/@floating-ui/utils": {
+      "version": "0.2.12",
+      "resolved": "https://registry.npmjs.org/@floating-ui/utils/-/utils-0.2.12.tgz",
+      "integrity": "sha512-HpCo8tmWzLVad5s2d19EhAz5zqrrQ6s69qd6moPMQvkOuSwDT1YgRfWSVuc4ennqrgv3OHppiOGMQ7oC13yIww==",
+      "license": "MIT"
+    },
     "node_modules/@img/colour": {
       "version": "1.1.0",
       "resolved": "https://registry.npmjs.org/@img/colour/-/colour-1.1.0.tgz",
       "integrity": "sha512-Td76q7j57o/tLVdgS746cYARfSyxk8iEfRxewL9h4OMzYhbW4TAcppl0mT4eyqXddh6L/jwoM75mo7ixa/pCeQ==",
       "license": "MIT",
       "optional": true,
       "engines": {
         "node": ">=18"
       }
     },
@@ -1045,27 +1104,65 @@
       "os": [
         "win32"
       ],
       "engines": {
         "node": ">=20.9.0"
       },
       "funding": {
         "url": "https://opencollective.com/libvips"
       }
     },
+    "node_modules/@jridgewell/gen-mapping": {
+      "version": "0.3.13",
+      "resolved": "https://registry.npmjs.org/@jridgewell/gen-mapping/-/gen-mapping-0.3.13.tgz",
+      "integrity": "sha512-2kkt/7niJ6MgEPxF0bYdQ6etZaA+fQvDcLKckhy1yIQOzaoKjBBjSj63/aLVjYE3qhRt5dvM+uUyfCg6UKCBbA==",
+      "license": "MIT",
+      "dependencies": {
+        "@jridgewell/sourcemap-codec": "^1.5.0",
+        "@jridgewell/trace-mapping": "^0.3.24"
+      }
+    },
+    "node_modules/@jridgewell/remapping": {
+      "version": "2.3.5",
+      "resolved": "https://registry.npmjs.org/@jridgewell/remapping/-/remapping-2.3.5.tgz",
+      "integrity": "sha512-LI9u/+laYG4Ds1TDKSJW2YPrIlcVYOwi2fUC6xB43lueCjgxV4lffOCZCtYFiH6TNOX+tQKXx97T4IKHbhyHEQ==",
+      "license": "MIT",
+      "dependencies": {
+        "@jridgewell/gen-mapping": "^0.3.5",
+        "@jridgewell/trace-mapping": "^0.3.24"
+      }
+    },
+    "node_modules/@jridgewell/resolve-uri": {
+      "version": "3.1.2",
+      "resolved": "https://registry.npmjs.org/@jridgewell/resolve-uri/-/resolve-uri-3.1.2.tgz",
+      "integrity": "sha512-bRISgCIjP20/tbWSPWMEi54QVPRZExkuD9lJL+UIxUKtwVJA8wW1Trb1jMs1RFXo1CBTNZ/5hpC9QvmKWdopKw==",
+      "license": "MIT",
+      "engines": {
+        "node": ">=6.0.0"
+      }
+    },
     "node_modules/@jridgewell/sourcemap-codec": {
       "version": "1.6.0",
       "resolved": "https://registry.npmjs.org/@jridgewell/sourcemap-codec/-/sourcemap-codec-1.6.0.tgz",
       "integrity": "sha512-T7jf+5zgsZHwNJ4lvQ7/aezbyk0nNX+zJVWpmHA7VYsEx7a7qr5Rg5IbtJFqkgze5Y2sruq1RUY8Q837Od7iFw==",
-      "dev": true,
       "license": "MIT"
     },
+    "node_modules/@jridgewell/trace-mapping": {
+      "version": "0.3.31",
+      "resolved": "https://registry.npmjs.org/@jridgewell/trace-mapping/-/trace-mapping-0.3.31.tgz",
+      "integrity": "sha512-zzNR+SdQSDJzc8joaeP8QQoCQr8NuYx2dIIytl1QeBEZHJ9uW6hebsrYgbz8hJwUQao3TWCMtmfV8Nu1twOLAw==",
+      "license": "MIT",
+      "dependencies": {
+        "@jridgewell/resolve-uri": "^3.1.0",
+        "@jridgewell/sourcemap-codec": "^1.4.14"
+      }
+    },
     "node_modules/@napi-rs/lzma-linux-x64-gnu": {
       "version": "1.5.1",
       "resolved": "https://registry.npmjs.org/@napi-rs/lzma-linux-x64-gnu/-/lzma-linux-x64-gnu-1.5.1.tgz",
       "integrity": "sha512-oTXEIha4SsuXdTA4Iyskj0kpdx2yVXdhd75c2v3xGrHFfVMsbhTPZU/nMPL4sWKo4pBHm3aucLaqGlF696dTyQ==",
       "cpu": [
         "x64"
       ],
       "dev": true,
       "libc": [
         "glibc"
@@ -1227,170 +1324,1666 @@
     },
     "node_modules/@panva/hkdf": {
       "version": "1.2.1",
       "resolved": "https://registry.npmjs.org/@panva/hkdf/-/hkdf-1.2.1.tgz",
       "integrity": "sha512-6oclG6Y3PiDFcoyk8srjLfVKyMfVCKJ27JwNPViuXziFpmdz+MZnZN/aKY0JGXgYuO/VghU0jcOAZgWXZ1Dmrw==",
       "license": "MIT",
       "funding": {
         "url": "https://github.com/sponsors/panva"
       }
     },
-    "node_modules/@rollup/rollup-android-arm-eabi": {
-      "version": "4.63.3",
-      "resolved": "https://registry.npmjs.org/@rollup/rollup-android-arm-eabi/-/rollup-android-arm-eabi-4.63.3.tgz",
-      "integrity": "sha512-w3Jnvi1ocaVm/c7yVPpfB98XeSRBMyzp6njL5MVVbGyXjpmUkN+s6Hp4t0PqhGCCaI1ZHMKXt/w0lA1RCaLVcw==",
-      "cpu": [
-        "arm"
-      ],
-      "dev": true,
-      "license": "MIT",
-      "optional": true,
-      "os": [
-        "android"
-      ]
+    "node_modules/@radix-ui/number": {
+      "version": "1.1.3",
+      "resolved": "https://registry.npmjs.org/@radix-ui/number/-/number-1.1.3.tgz",
+      "integrity": "sha512-Road2bidD0uu/1BGDOWNdPI06g0lIRy6IF9GZcIrDK2KGItfor8IQwQa+yM2ERgHM1MmHxaxpTzk0/Jp42lNfA==",
+      "license": "MIT"
     },
-    "node_modules/@rollup/rollup-android-arm64": {
-      "version": "4.63.3",
-      "resolved": "https://registry.npmjs.org/@rollup/rollup-android-arm64/-/rollup-android-arm64-4.63.3.tgz",
-      "integrity": "sha512-uI/ESiaIbbRYAEhzy8PCUWDp1hB0bjAqM06mW9flOoNO4Q8DQpeoREhBR5Hegfl+wpXiguyJv6XSPzEN7OxyHQ==",
-      "cpu": [
-        "arm64"
-      ],
-      "dev": true,
+    "node_modules/@radix-ui/primitive": {
+      "version": "1.1.7",
+      "resolved": "https://registry.npmjs.org/@radix-ui/primitive/-/primitive-1.1.7.tgz",
+      "integrity": "sha512-rqWnm76nYT8HoNNqEjpgJ7Pw/DrBj5iBTrmEPo6HTX5+VJyBNOqTdv4g89G63HuR5g0AaENoAcH7Is5fF2kZ8Q==",
+      "license": "MIT"
+    },
+    "node_modules/@radix-ui/react-accessible-icon": {
+      "version": "1.1.15",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-accessible-icon/-/react-accessible-icon-1.1.15.tgz",
+      "integrity": "sha512-WTQwcAvQf5sOcuUyi90lKPbhwcvQ+j55cjrSmeaN+L2vKU3DooOvlKw2MDeiJ5IkV5N905KW0/fGojKOBhD11A==",
       "license": "MIT",
-      "optional": true,
-      "os": [
-        "android"
-      ]
+      "dependencies": {
+        "@radix-ui/react-visually-hidden": "1.2.11"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
     },
-    "node_modules/@rollup/rollup-darwin-arm64": {
-      "version": "4.63.3",
-      "resolved": "https://registry.npmjs.org/@rollup/rollup-darwin-arm64/-/rollup-darwin-arm64-4.63.3.tgz",
-      "integrity": "sha512-oxhrd1jmXLwWZ83eQYDXxuqRdkqkzrjR3JobKeuUyfdNZo11FuQIvqEOZhyIT7OBHxXoGslDDjN0cQcM6T0TqQ==",
-      "cpu": [
-        "arm64"
-      ],
-      "dev": true,
+    "node_modules/@radix-ui/react-accordion": {
+      "version": "1.2.20",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-accordion/-/react-accordion-1.2.20.tgz",
+      "integrity": "sha512-jDhG9FvAEnlhnjrsINbNXcUa4G+L1KqSkJSunkbKEzFRcAb52jvM0PjPxPRvhe1HNc5F5yc0yzzWeeqlH4yBIg==",
       "license": "MIT",
-      "optional": true,
-      "os": [
-        "darwin"
-      ]
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-collapsible": "1.1.20",
+        "@radix-ui/react-collection": "1.1.15",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-direction": "1.1.4",
+        "@radix-ui/react-id": "1.1.4",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-use-controllable-state": "1.2.6"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
     },
-    "node_modules/@rollup/rollup-darwin-x64": {
-      "version": "4.63.3",
-      "resolved": "https://registry.npmjs.org/@rollup/rollup-darwin-x64/-/rollup-darwin-x64-4.63.3.tgz",
-      "integrity": "sha512-7/YiIMghVE8DrxKvNdorAaJVdriOFgOIpdStnPx8ppx5zfTwC3jBCSEAIzB7JD5404m65THl6H93UTTVUvypmg==",
-      "cpu": [
-        "x64"
-      ],
-      "dev": true,
+    "node_modules/@radix-ui/react-alert-dialog": {
+      "version": "1.1.23",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-alert-dialog/-/react-alert-dialog-1.1.23.tgz",
+      "integrity": "sha512-VAYOiQRqj3GPpYJE0I9J+X8Ip05cyVlNdKOFeiGS2Ou1HHGfpl0BxOyZm6nmVDyU+W+NF3/XLzmjHmVGydhwgA==",
       "license": "MIT",
-      "optional": true,
-      "os": [
-        "darwin"
-      ]
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-dialog": "1.1.23",
+        "@radix-ui/react-primitive": "2.1.10"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
     },
-    "node_modules/@rollup/rollup-freebsd-arm64": {
-      "version": "4.63.3",
-      "resolved": "https://registry.npmjs.org/@rollup/rollup-freebsd-arm64/-/rollup-freebsd-arm64-4.63.3.tgz",
-      "integrity": "sha512-GXFZRRoMAytaI5z6N3Zhfw0WL18Q0M8r95D5hlC4GqE/lGk8pbSJNUBoOWDfbm6dTciqHj2nU87tI5f6XhQiOg==",
-      "cpu": [
-        "arm64"
-      ],
-      "dev": true,
+    "node_modules/@radix-ui/react-arrow": {
+      "version": "1.1.15",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-arrow/-/react-arrow-1.1.15.tgz",
+      "integrity": "sha512-v4zggRcjadnI+ClKDuijlQEW4tw3NoaeHc/PwpKnLoLLKNUG4InLegkstooLcRIUWCs+8L22dGURCVuFfOKfnA==",
       "license": "MIT",
-      "optional": true,
-      "os": [
-        "freebsd"
-      ]
+      "dependencies": {
+        "@radix-ui/react-primitive": "2.1.10"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
     },
-    "node_modules/@rollup/rollup-freebsd-x64": {
-      "version": "4.63.3",
-      "resolved": "https://registry.npmjs.org/@rollup/rollup-freebsd-x64/-/rollup-freebsd-x64-4.63.3.tgz",
-      "integrity": "sha512-77W+8X3ddYgPxUpB8nZFQs2Mq+wc4HVlcSRtApXLjYBcnPMkttrSnU8VwKQjeWYhMsITHFs5cWBQ8vz1Q+5RHQ==",
-      "cpu": [
-        "x64"
-      ],
-      "dev": true,
+    "node_modules/@radix-ui/react-aspect-ratio": {
+      "version": "1.1.15",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-aspect-ratio/-/react-aspect-ratio-1.1.15.tgz",
+      "integrity": "sha512-fy+dyVR+90nelK8rqIznFlxzx7uPcGbhxH8Nfr2bHb4UfSe+e3hklOC0luK0hDwVwnRX7xTRySpsrQVeW+/oNQ==",
       "license": "MIT",
-      "optional": true,
-      "os": [
-        "freebsd"
-      ]
+      "dependencies": {
+        "@radix-ui/react-primitive": "2.1.10"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
     },
-    "node_modules/@rollup/rollup-linux-arm-gnueabihf": {
-      "version": "4.63.3",
-      "resolved": "https://registry.npmjs.org/@rollup/rollup-linux-arm-gnueabihf/-/rollup-linux-arm-gnueabihf-4.63.3.tgz",
-      "integrity": "sha512-FVkwK+iUC+mq+GipVK46rRVticfAPtvPUNlqlGXUDxdVk/UGjQiiiUVPUrEXdSpU2ufU0XxLGyTqDtBidDOVmg==",
-      "cpu": [
-        "arm"
-      ],
-      "dev": true,
-      "libc": [
-        "glibc"
-      ],
+    "node_modules/@radix-ui/react-avatar": {
+      "version": "1.2.6",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-avatar/-/react-avatar-1.2.6.tgz",
+      "integrity": "sha512-4ULOTJ/mqy2hT9GlWa/MFHxHSvH3nJzHnZM1waNsc5Bonv7i70aNenghXmD97S6OJ81ekXONGGt4nT1r0PfEdA==",
       "license": "MIT",
-      "optional": true,
-      "os": [
-        "linux"
-      ]
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-use-callback-ref": "1.1.4",
+        "@radix-ui/react-use-is-hydrated": "0.1.3",
+        "@radix-ui/react-use-layout-effect": "1.1.4"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
     },
-    "node_modules/@rollup/rollup-linux-arm-musleabihf": {
-      "version": "4.63.3",
-      "resolved": "https://registry.npmjs.org/@rollup/rollup-linux-arm-musleabihf/-/rollup-linux-arm-musleabihf-4.63.3.tgz",
-      "integrity": "sha512-+aGU1t3398yQOVj1Bz8o3e+KtswxAPvO+mtxtNdfXYMkXIHu7XhhkCD7/DEH9q8tF8uhDnMWvfpUKI8y1sZJsg==",
-      "cpu": [
-        "arm"
-      ],
-      "dev": true,
-      "libc": [
-        "musl"
-      ],
+    "node_modules/@radix-ui/react-checkbox": {
+      "version": "1.3.11",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-checkbox/-/react-checkbox-1.3.11.tgz",
+      "integrity": "sha512-Gnptr9pDDQxD3hgq2dtPbtrp/c2qH1mBwIzw3X/ivrMb2e1t0jMTi606fVEqFPaQR1ggXIVQWKj3P2WW9v7zGQ==",
       "license": "MIT",
-      "optional": true,
-      "os": [
-        "linux"
-      ]
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-presence": "1.1.10",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-use-controllable-state": "1.2.6",
+        "@radix-ui/react-use-size": "1.1.4"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
     },
-    "node_modules/@rollup/rollup-linux-arm64-gnu": {
-      "version": "4.63.3",
-      "resolved": "https://registry.npmjs.org/@rollup/rollup-linux-arm64-gnu/-/rollup-linux-arm64-gnu-4.63.3.tgz",
-      "integrity": "sha512-cR0kjpRXR2KJ2oQK8E2KTPtphs+b9hZ8IhTZubNryt/RsqgdOZBQ2Zq0q5UedtiIi0rs3jVhJh55RE1ZHUVGUA==",
-      "cpu": [
-        "arm64"
-      ],
-      "dev": true,
-      "libc": [
-        "glibc"
-      ],
+    "node_modules/@radix-ui/react-collapsible": {
+      "version": "1.1.20",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-collapsible/-/react-collapsible-1.1.20.tgz",
+      "integrity": "sha512-mcGesGplBnzN2sbvJETzpCNfSMyPnb29q1GRLU+Ib7bJrpIG2ywmRoh2V5VbA2uNvKikKUlVbAPks7JDjz4A8Q==",
       "license": "MIT",
-      "optional": true,
-      "os": [
-        "linux"
-      ]
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-id": "1.1.4",
+        "@radix-ui/react-presence": "1.1.10",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-use-controllable-state": "1.2.6",
+        "@radix-ui/react-use-layout-effect": "1.1.4"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
     },
-    "node_modules/@rollup/rollup-linux-arm64-musl": {
-      "version": "4.63.3",
-      "resolved": "https://registry.npmjs.org/@rollup/rollup-linux-arm64-musl/-/rollup-linux-arm64-musl-4.63.3.tgz",
-      "integrity": "sha512-y1RYi4Q3/9ByVWSSt9kX2ustE0B7kFYbJ6zZdVZVyqopZs3yhCTwRfrjIX4vezUJInma/Gs6BOFDJg7yZmJ0IQ==",
-      "cpu": [
-        "arm64"
-      ],
-      "dev": true,
-      "libc": [
-        "musl"
-      ],
+    "node_modules/@radix-ui/react-collection": {
+      "version": "1.1.15",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-collection/-/react-collection-1.1.15.tgz",
+      "integrity": "sha512-9W+B9NPF0NaaPh/1NJd3+KqsnlLqU9H7T2rvww+fp+T/evVXdNAyYcnfRQZFOjkR1ajQp3yORlqnI8soawLvNA==",
       "license": "MIT",
-      "optional": true,
-      "os": [
-        "linux"
+      "dependencies": {
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-slot": "1.3.3"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-compose-refs": {
+      "version": "1.1.5",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-compose-refs/-/react-compose-refs-1.1.5.tgz",
+      "integrity": "sha512-+48PbAAbq3didjJxa+OaWY2ZwgAKsNiRGyeHKszblZMQ+kcpd9pAaT11cMkGEie0vsOi3QdeTE6d5Fe3Gn61kA==",
+      "license": "MIT",
+      "peerDependencies": {
+        "@types/react": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-context": {
+      "version": "1.2.2",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-context/-/react-context-1.2.2.tgz",
+      "integrity": "sha512-RHCUGwKHDr0hDGg4X7ma4JG4/+12qxw8rkh5QKdDldlCvtja6nUx1Ef/8HVrJze81lEsgLQlqjzjGNHantgnQA==",
+      "license": "MIT",
+      "peerDependencies": {
+        "@types/react": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-context-menu": {
+      "version": "2.3.7",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-context-menu/-/react-context-menu-2.3.7.tgz",
+      "integrity": "sha512-CtXP35dxaB5T3zXSd+E3uHe/QpXcpYnZmxp6OaIbfthtfW4wyb77M23BG+bwIJDtsMwEP/YssdsmNyZu7jhWew==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-menu": "2.1.24",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-use-controllable-state": "1.2.6"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-dialog": {
+      "version": "1.1.23",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-dialog/-/react-dialog-1.1.23.tgz",
+      "integrity": "sha512-Ksw4WeROkO4rC9k/onilX/Ao2Cr1ku1unMNH+XSCcP4jSXYu7HDsg9n4ojMjVb22XpYjAQ9qfrFlVbru1vXDUA==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-dismissable-layer": "1.1.19",
+        "@radix-ui/react-focus-guards": "1.1.6",
+        "@radix-ui/react-focus-scope": "1.1.16",
+        "@radix-ui/react-id": "1.1.4",
+        "@radix-ui/react-portal": "1.1.17",
+        "@radix-ui/react-presence": "1.1.10",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-slot": "1.3.3",
+        "@radix-ui/react-use-controllable-state": "1.2.6",
+        "@radix-ui/react-use-layout-effect": "1.1.4",
+        "aria-hidden": "^1.2.4",
+        "react-remove-scroll": "^2.7.2"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-direction": {
+      "version": "1.1.4",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-direction/-/react-direction-1.1.4.tgz",
+      "integrity": "sha512-5pzg4FGQNpExhnhT2zlrP1wZFaYCd1K0nYWoFAdcYoYK868IEigqMX3B3f8yIoRlAhAeDWciLI6ZdCKHF9P4Vg==",
+      "license": "MIT",
+      "peerDependencies": {
+        "@types/react": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-dismissable-layer": {
+      "version": "1.1.19",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-dismissable-layer/-/react-dismissable-layer-1.1.19.tgz",
+      "integrity": "sha512-8g4pfOL9HoKKLWGiypT+dphVqjFfmcXO5GBnhsG6zI+lxAx/8feQpr+1LSN8Re3hiZ+XkLNS4O9ztK11/LzQ6w==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-use-callback-ref": "1.1.4",
+        "@radix-ui/react-use-effect-event": "0.0.5"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-dropdown-menu": {
+      "version": "2.1.24",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-dropdown-menu/-/react-dropdown-menu-2.1.24.tgz",
+      "integrity": "sha512-geq8l2rJkxvkXsT9RMgtUE3P8pITFpTsvYpbySi1IH4fZEABD/Gp85myayFgxk0ktljGMJnCbeFkyTusvSvv7g==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-id": "1.1.4",
+        "@radix-ui/react-menu": "2.1.24",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-use-controllable-state": "1.2.6"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-focus-guards": {
+      "version": "1.1.6",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-focus-guards/-/react-focus-guards-1.1.6.tgz",
+      "integrity": "sha512-RNOJjfZMTyBM6xYmV3IVGXkPjIhcBAuv48POevAXwrGJhkWZ9p1rFoIS1JFooPuT193AZmRsCPhpoVJxx6OPoQ==",
+      "license": "MIT",
+      "peerDependencies": {
+        "@types/react": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-focus-scope": {
+      "version": "1.1.16",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-focus-scope/-/react-focus-scope-1.1.16.tgz",
+      "integrity": "sha512-wmRZ2WWLvmt6KHy2rNPOdPUjwq5xOHY02+m+udwJTn0aNIox/rkskAvJTyTLGhPK6KgrUjlJUJpgmx/+wFiFIQ==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-use-callback-ref": "1.1.4"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-form": {
+      "version": "0.1.16",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-form/-/react-form-0.1.16.tgz",
+      "integrity": "sha512-Q4TLEn2A7TAypxwmd6R9EwrlXDvkfYSDMrq9/887AXAGh+G1rH+kYJKSTv+Si9Y0JPKTwKYv6PviAJosysNimA==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-id": "1.1.4",
+        "@radix-ui/react-label": "2.1.15",
+        "@radix-ui/react-primitive": "2.1.10"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-hover-card": {
+      "version": "1.1.23",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-hover-card/-/react-hover-card-1.1.23.tgz",
+      "integrity": "sha512-H8qONfZd3ltrU3+jHCIgITbWo6e1iTKvP9DHdrvYbX48ooRM5FjEDTn16AMwdfuOGkWdZEhpl3PLL/Wk/AnHDQ==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-dismissable-layer": "1.1.19",
+        "@radix-ui/react-popper": "1.3.7",
+        "@radix-ui/react-portal": "1.1.17",
+        "@radix-ui/react-presence": "1.1.10",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-use-controllable-state": "1.2.6"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-id": {
+      "version": "1.1.4",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-id/-/react-id-1.1.4.tgz",
+      "integrity": "sha512-TMQp2llA+RYn7JcjnrMnz7wN4pcVttPZnRZo52PLQsoLVKzNlVwUeHmfePgTgRluXFvlD3GD5g5MOVVTJCO0qA==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/react-use-layout-effect": "1.1.4"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-label": {
+      "version": "2.1.15",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-label/-/react-label-2.1.15.tgz",
+      "integrity": "sha512-o/rdYEwZTTo5tjknnPeyQFU45kUC4i/XyeDPP+HGyi6XqpOP6Zf5Ya5vh/Yfe9Id5JiuWnnAx2XqIeD3UYZt0g==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/react-primitive": "2.1.10"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-menu": {
+      "version": "2.1.24",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-menu/-/react-menu-2.1.24.tgz",
+      "integrity": "sha512-uW7RVuU6Lp/ZtfeY4b3kL32zccgEWvPv1+cf17ubYzHa9cL8AHokmk36cG/XEiH/smbQvumnieXX9j/e9RqJWA==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-collection": "1.1.15",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-direction": "1.1.4",
+        "@radix-ui/react-dismissable-layer": "1.1.19",
+        "@radix-ui/react-focus-guards": "1.1.6",
+        "@radix-ui/react-focus-scope": "1.1.16",
+        "@radix-ui/react-id": "1.1.4",
+        "@radix-ui/react-popper": "1.3.7",
+        "@radix-ui/react-portal": "1.1.17",
+        "@radix-ui/react-presence": "1.1.10",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-roving-focus": "1.1.19",
+        "@radix-ui/react-slot": "1.3.3",
+        "@radix-ui/react-use-callback-ref": "1.1.4",
+        "aria-hidden": "^1.2.4",
+        "react-remove-scroll": "^2.7.2"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-menubar": {
+      "version": "1.1.24",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-menubar/-/react-menubar-1.1.24.tgz",
+      "integrity": "sha512-eeVs0vf7cuqXaM0qLQCPcufImiJNVBXdJDLu7ZGYl2732UH23Qat/foNGrr6vYV3/DdTsBqASoggUFgH14OcZA==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-collection": "1.1.15",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-direction": "1.1.4",
+        "@radix-ui/react-id": "1.1.4",
+        "@radix-ui/react-menu": "2.1.24",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-roving-focus": "1.1.19",
+        "@radix-ui/react-use-controllable-state": "1.2.6"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-navigation-menu": {
+      "version": "1.2.22",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-navigation-menu/-/react-navigation-menu-1.2.22.tgz",
+      "integrity": "sha512-ou7iLEJ+yrhQndkkA4U21XIdS/CS45F4iXIkTZcb6/Ne9EMsOuDudVmCwmDnfFZZ+y1FZqXRNSIgBy+YMvZVZg==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-collection": "1.1.15",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-direction": "1.1.4",
+        "@radix-ui/react-dismissable-layer": "1.1.19",
+        "@radix-ui/react-id": "1.1.4",
+        "@radix-ui/react-presence": "1.1.10",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-use-callback-ref": "1.1.4",
+        "@radix-ui/react-use-controllable-state": "1.2.6",
+        "@radix-ui/react-use-layout-effect": "1.1.4",
+        "@radix-ui/react-use-previous": "1.1.4",
+        "@radix-ui/react-visually-hidden": "1.2.11"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-one-time-password-field": {
+      "version": "0.1.16",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-one-time-password-field/-/react-one-time-password-field-0.1.16.tgz",
+      "integrity": "sha512-Tj9P6ntAJEw52oq/F0AGknXR4XncxEt7XU47O3xJQOiWfLzEy3d9gtgKfvjSzGxzHkfL+VzvxGu2KTFsloJqXw==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/number": "1.1.3",
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-collection": "1.1.15",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-direction": "1.1.4",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-roving-focus": "1.1.19",
+        "@radix-ui/react-use-controllable-state": "1.2.6",
+        "@radix-ui/react-use-effect-event": "0.0.5",
+        "@radix-ui/react-use-is-hydrated": "0.1.3",
+        "@radix-ui/react-use-layout-effect": "1.1.4"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-password-toggle-field": {
+      "version": "0.1.11",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-password-toggle-field/-/react-password-toggle-field-0.1.11.tgz",
+      "integrity": "sha512-4gvFnmDXu3dgj21CqsufzIameRvlRd4SBqaWhcrlrNhRo0Y5i/49AmRJYe1fdAM3G2VNBbmin4b0D6cdQocwgw==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-id": "1.1.4",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-use-controllable-state": "1.2.6",
+        "@radix-ui/react-use-effect-event": "0.0.5",
+        "@radix-ui/react-use-is-hydrated": "0.1.3"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-popover": {
+      "version": "1.1.23",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-popover/-/react-popover-1.1.23.tgz",
+      "integrity": "sha512-mw58MrBlyHWFisTOYignD0vf/3gdcgAR+9of1s9G/38CbFiUwH1nCDkc0AUM9IrXFgN5Ue8n45j9WCgyM1sbiQ==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-dismissable-layer": "1.1.19",
+        "@radix-ui/react-focus-guards": "1.1.6",
+        "@radix-ui/react-focus-scope": "1.1.16",
+        "@radix-ui/react-id": "1.1.4",
+        "@radix-ui/react-popper": "1.3.7",
+        "@radix-ui/react-portal": "1.1.17",
+        "@radix-ui/react-presence": "1.1.10",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-slot": "1.3.3",
+        "@radix-ui/react-use-controllable-state": "1.2.6",
+        "aria-hidden": "^1.2.4",
+        "react-remove-scroll": "^2.7.2"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-popper": {
+      "version": "1.3.7",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-popper/-/react-popper-1.3.7.tgz",
+      "integrity": "sha512-UsJrrd7w4wuKKTdvd/DNERVlwSlUcyXzjhyDwBk+3aPOsCjOY6ZSbxuw8E6lZTjjfP8Cpd0J8VVkrYUWyGYXyg==",
+      "license": "MIT",
+      "dependencies": {
+        "@floating-ui/react-dom": "^2.0.0",
+        "@radix-ui/react-arrow": "1.1.15",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-use-callback-ref": "1.1.4",
+        "@radix-ui/react-use-layout-effect": "1.1.4",
+        "@radix-ui/react-use-rect": "1.1.4",
+        "@radix-ui/react-use-size": "1.1.4",
+        "@radix-ui/rect": "1.1.3"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-portal": {
+      "version": "1.1.17",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-portal/-/react-portal-1.1.17.tgz",
+      "integrity": "sha512-vKQLcWypUnwZVvfV7UkGahH2g6ySe8M8R+zYBwPrv5byZ9QAW6cQVvNKo7GgmD+p8aYb6D9JBuvy8/WhOno2wQ==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-use-layout-effect": "1.1.4"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-presence": {
+      "version": "1.1.10",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-presence/-/react-presence-1.1.10.tgz",
+      "integrity": "sha512-3wyzCQ6+ubRA+D4uv9m95JYLXxmOHp05qjrkjeA7uKHHtjpPggQzc6DAb0URl7j67oR0K2foO4ip27TiX037Bw==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/react-use-layout-effect": "1.1.4"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-primitive": {
+      "version": "2.1.10",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-primitive/-/react-primitive-2.1.10.tgz",
+      "integrity": "sha512-MucOnzh6hR5mid6VpkbglRAMYMjKLqRnGBbjXkzjK52fuQDd1qbkx78a5P40mkcnVXJdEVxm26E9OPAiUq7nBg==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/react-slot": "1.3.3"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-progress": {
+      "version": "1.1.16",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-progress/-/react-progress-1.1.16.tgz",
+      "integrity": "sha512-5XnomAsoZZCY+KNTxbIghpGqPruZvKFNlvcAljVAOdDRDsH4/OZQxhtwo5wdtoDM5R6MhJBb2sPnDuRFep3lzg==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-primitive": "2.1.10"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-radio-group": {
+      "version": "1.4.7",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-radio-group/-/react-radio-group-1.4.7.tgz",
+      "integrity": "sha512-cgYFEkntCxppHZgtSZ+7vh0wbZQ+IC7PPMw8DSnRG27B6kDd32/Zw0OJt7dGDigCoprMuWHjg2PvUn3PYvPFoQ==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-direction": "1.1.4",
+        "@radix-ui/react-presence": "1.1.10",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-roving-focus": "1.1.19",
+        "@radix-ui/react-use-controllable-state": "1.2.6",
+        "@radix-ui/react-use-size": "1.1.4"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-roving-focus": {
+      "version": "1.1.19",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-roving-focus/-/react-roving-focus-1.1.19.tgz",
+      "integrity": "sha512-V9jI6hDjT7l3jsCQD9bLNvDLM3tH/gdbOTp7Tefp3hbbgCGQoK7tUvrWiRlcoBHIZ809ElXwNQwVo0B98LuTXQ==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-collection": "1.1.15",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-direction": "1.1.4",
+        "@radix-ui/react-id": "1.1.4",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-use-callback-ref": "1.1.4",
+        "@radix-ui/react-use-controllable-state": "1.2.6",
+        "@radix-ui/react-use-is-hydrated": "0.1.3",
+        "@radix-ui/react-use-layout-effect": "1.1.4"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-scroll-area": {
+      "version": "1.2.18",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-scroll-area/-/react-scroll-area-1.2.18.tgz",
+      "integrity": "sha512-Zn5Cd171wxsO3Dfg8HaW6RifTb9CYTKQJHs/G4+LN1GfmJpaQMZQyQxMprVPHpaz7QY4l9BxK2JwQuzHsXC8nA==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/number": "1.1.3",
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-direction": "1.1.4",
+        "@radix-ui/react-presence": "1.1.10",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-use-callback-ref": "1.1.4",
+        "@radix-ui/react-use-layout-effect": "1.1.4"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-select": {
+      "version": "2.3.7",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-select/-/react-select-2.3.7.tgz",
+      "integrity": "sha512-WFGImkmbzcfxeIwq/+4HvRN0pizBwbwQUED4I13ezQsDdfl38ZntN6TmR8XaSzPBqoCToe8rF75j6NPNDSzhbg==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/number": "1.1.3",
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-collection": "1.1.15",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-direction": "1.1.4",
+        "@radix-ui/react-dismissable-layer": "1.1.19",
+        "@radix-ui/react-focus-guards": "1.1.6",
+        "@radix-ui/react-focus-scope": "1.1.16",
+        "@radix-ui/react-id": "1.1.4",
+        "@radix-ui/react-popper": "1.3.7",
+        "@radix-ui/react-portal": "1.1.17",
+        "@radix-ui/react-presence": "1.1.10",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-slot": "1.3.3",
+        "@radix-ui/react-use-callback-ref": "1.1.4",
+        "@radix-ui/react-use-controllable-state": "1.2.6",
+        "@radix-ui/react-use-layout-effect": "1.1.4",
+        "@radix-ui/react-use-previous": "1.1.4",
+        "@radix-ui/react-visually-hidden": "1.2.11",
+        "aria-hidden": "^1.2.4",
+        "react-remove-scroll": "^2.7.2"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-separator": {
+      "version": "1.1.15",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-separator/-/react-separator-1.1.15.tgz",
+      "integrity": "sha512-jOLO4lssEzWpoDu7G+Ze4VjwMRUBt291pnZD0gmalREZipnTX3wadQo7Fy48GCTfe14/YRN6rw/rOJqrE85Wxw==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/react-primitive": "2.1.10"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-slider": {
+      "version": "1.4.7",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-slider/-/react-slider-1.4.7.tgz",
+      "integrity": "sha512-mTSLf1GC/C0moWjTbvCM6Qn/gBjvlFt1azuWF2v7MN5C3Zq2U2J2lN3ZEYkpujuOU5Ro7A28wkviSxaKnG0BYg==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/number": "1.1.3",
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-collection": "1.1.15",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-direction": "1.1.4",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-use-controllable-state": "1.2.6",
+        "@radix-ui/react-use-layout-effect": "1.1.4",
+        "@radix-ui/react-use-previous": "1.1.4",
+        "@radix-ui/react-use-size": "1.1.4"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-slot": {
+      "version": "1.3.3",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-slot/-/react-slot-1.3.3.tgz",
+      "integrity": "sha512-qx7oqnYbxnK9kYI9m317qmFmEgo6ywqWvbTogdj7cL9p3/yx4M48p7Rnw5z3H890cL/ow/EeWJsuTykeZVXP5Q==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/react-compose-refs": "1.1.5"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-switch": {
+      "version": "1.3.7",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-switch/-/react-switch-1.3.7.tgz",
+      "integrity": "sha512-48tB/4dn2UVLBCYhTu9AuR63IHl73l/qLbLgxd86noTUor4/K4LFDAcYjK+isP5313qxaFpjPVogE7+Y0/V3Kw==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-use-controllable-state": "1.2.6",
+        "@radix-ui/react-use-size": "1.1.4"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-tabs": {
+      "version": "1.1.21",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-tabs/-/react-tabs-1.1.21.tgz",
+      "integrity": "sha512-UKxJlZid7FVtsk/WTxj4i4uSEgj2Au+KBbS7SQyTlzMhhn+86Cz3tISZdTa87bfEfcuvZezf2ZsxD4xuEKtkog==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-direction": "1.1.4",
+        "@radix-ui/react-id": "1.1.4",
+        "@radix-ui/react-presence": "1.1.10",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-roving-focus": "1.1.19",
+        "@radix-ui/react-use-controllable-state": "1.2.6"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-toast": {
+      "version": "1.2.23",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-toast/-/react-toast-1.2.23.tgz",
+      "integrity": "sha512-ofhyAsYaocRGOs/n0XWdUOSVzEAG6BfrMVM8z0c0kLEWY38w/0WuMFPTJP/HVaZPYkMvHZoKIIhNcjbTCBILPg==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-collection": "1.1.15",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-dismissable-layer": "1.1.19",
+        "@radix-ui/react-portal": "1.1.17",
+        "@radix-ui/react-presence": "1.1.10",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-use-callback-ref": "1.1.4",
+        "@radix-ui/react-use-controllable-state": "1.2.6",
+        "@radix-ui/react-use-layout-effect": "1.1.4",
+        "@radix-ui/react-visually-hidden": "1.2.11"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-toggle": {
+      "version": "1.1.18",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-toggle/-/react-toggle-1.1.18.tgz",
+      "integrity": "sha512-7lonPlKfSacd20GlOBx2ltuVKz9oqWYZz+oMQyOltw6t1y2nyftj2ZmwwUHYn49kqfDWcp8dNZm5NgV+5Z+mug==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-use-controllable-state": "1.2.6"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-toggle-group": {
+      "version": "1.1.19",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-toggle-group/-/react-toggle-group-1.1.19.tgz",
+      "integrity": "sha512-OtnwuSVjd1Ofi+AdnvhsjQdyuhCDwYs1w9RyB5BN/OavXOVQo42SYqQjwUnbPnaiPFBpQ9aX70dWeee+v2oBLA==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-direction": "1.1.4",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-roving-focus": "1.1.19",
+        "@radix-ui/react-toggle": "1.1.18",
+        "@radix-ui/react-use-controllable-state": "1.2.6"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-toolbar": {
+      "version": "1.1.19",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-toolbar/-/react-toolbar-1.1.19.tgz",
+      "integrity": "sha512-Ph0IvtYw4VB12ZnZg+YtrGs8yJQsnizwo/zu0R4Y/nWugtJzA7Pg1eWeuDR9+LSqn+xjamss+UOSOJJJ4gx8jw==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-direction": "1.1.4",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-roving-focus": "1.1.19",
+        "@radix-ui/react-separator": "1.1.15",
+        "@radix-ui/react-toggle-group": "1.1.19"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-tooltip": {
+      "version": "1.2.16",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-tooltip/-/react-tooltip-1.2.16.tgz",
+      "integrity": "sha512-6EamKFRRnlpdadndbZ6LMwycfwkwPte1B42hs6QA0gYhjaOKqW4PZ4pjaW9UrlDX5eVt/OjncE7BFTPL5nmZhg==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-dismissable-layer": "1.1.19",
+        "@radix-ui/react-id": "1.1.4",
+        "@radix-ui/react-popper": "1.3.7",
+        "@radix-ui/react-portal": "1.1.17",
+        "@radix-ui/react-presence": "1.1.10",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-slot": "1.3.3",
+        "@radix-ui/react-use-controllable-state": "1.2.6",
+        "@radix-ui/react-use-layout-effect": "1.1.4",
+        "@radix-ui/react-visually-hidden": "1.2.11"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-use-callback-ref": {
+      "version": "1.1.4",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-use-callback-ref/-/react-use-callback-ref-1.1.4.tgz",
+      "integrity": "sha512-R6OUY2e2fA6Yn6s+VSx5KBV6Nx8LQEhu+cz7LCej18rQ1HLyg9PSC9jP/ZNx0o6FAIK9c0F1kHylzSxKsdlkrQ==",
+      "license": "MIT",
+      "peerDependencies": {
+        "@types/react": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-use-controllable-state": {
+      "version": "1.2.6",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-use-controllable-state/-/react-use-controllable-state-1.2.6.tgz",
+      "integrity": "sha512-uEQJGT97ZA/TgP/Hydw47lHu+/vQj6z/0jA+WeTbK1o9Rx45GImjpD0tc3W5ad3D6XTSR6e1yEO0FvGq6WQfVQ==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-use-effect-event": "0.0.5",
+        "@radix-ui/react-use-layout-effect": "1.1.4"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-use-effect-event": {
+      "version": "0.0.5",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-use-effect-event/-/react-use-effect-event-0.0.5.tgz",
+      "integrity": "sha512-7cshFL8HGS/7HEiHH+9kL9HBwp2sa9yX18Knwek6KYWmXwM7pegMgta2AXMQKI+rq3JnfSj9x8wYqFMTdG1Jgg==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/react-use-layout-effect": "1.1.4"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-use-escape-keydown": {
+      "version": "1.1.5",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-use-escape-keydown/-/react-use-escape-keydown-1.1.5.tgz",
+      "integrity": "sha512-ge3ipobwSXTj4JyVtswQ7qZj0ZHdtbGuOno/LrgAAeSxtsJ6Vs4Gz5IkPH2bmqpjcLUFoqGhA/mueuIf63UXlA==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/react-use-callback-ref": "1.1.4"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-use-is-hydrated": {
+      "version": "0.1.3",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-use-is-hydrated/-/react-use-is-hydrated-0.1.3.tgz",
+      "integrity": "sha512-umO/aJ+82CpOnhDZUTbILCQf7kU/g0iv+oGs/Q8jw7IkhWBzaEP4sA268PhFAJTFetbwp3ICc6ktpI4TqtxcIw==",
+      "license": "MIT",
+      "peerDependencies": {
+        "@types/react": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-use-layout-effect": {
+      "version": "1.1.4",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-use-layout-effect/-/react-use-layout-effect-1.1.4.tgz",
+      "integrity": "sha512-K20DkRkUwDnxEYMBPcg3Y6voLkEy5p5QQmszZgLngKKiC7dzBR/aEuK3w1qlx2JWDUNH6FluahYdgR3BP+QbYw==",
+      "license": "MIT",
+      "peerDependencies": {
+        "@types/react": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-use-previous": {
+      "version": "1.1.4",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-use-previous/-/react-use-previous-1.1.4.tgz",
+      "integrity": "sha512-XoSLhbRbqxFtgJoi2fNHA3C6pDlY34x508vUpUGoFZfvePfHXHbE1lC4FYFMnJWgiCRroSTw6fOsXQoVS9RwZg==",
+      "license": "MIT",
+      "peerDependencies": {
+        "@types/react": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-use-rect": {
+      "version": "1.1.4",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-use-rect/-/react-use-rect-1.1.4.tgz",
+      "integrity": "sha512-cSOCh6JlkmfjLyNcLiu2nB4v+nm+dkZ+Q5KHWk/soo4U7ZLiEQFKHK9/YmtBHjfCEaU43IBKQOc4/uJmCaiCTQ==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/rect": "1.1.3"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-use-size": {
+      "version": "1.1.4",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-use-size/-/react-use-size-1.1.4.tgz",
+      "integrity": "sha512-D3anSY15EJoxrihpsXI6SMrmmonnQtR2ni7arO+Lfdg3O95b9hNXxONk8jA5C8ANdF/h5HMAxejgs8PWJ6rlhw==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/react-use-layout-effect": "1.1.4"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/react-visually-hidden": {
+      "version": "1.2.11",
+      "resolved": "https://registry.npmjs.org/@radix-ui/react-visually-hidden/-/react-visually-hidden-1.2.11.tgz",
+      "integrity": "sha512-NFS86RYYZb4/exihaESBGOpMJFz8MGLAfu3mOBSGByVnVPC9JPASfYubxd/8KbkQK0sYAv8lVQDEQukDX/qXvQ==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/react-primitive": "2.1.10"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/@radix-ui/rect": {
+      "version": "1.1.3",
+      "resolved": "https://registry.npmjs.org/@radix-ui/rect/-/rect-1.1.3.tgz",
+      "integrity": "sha512-JtyZR+mqgBibTo8xea3B6ZRmzZiM/YeVBtUkas6zMuXjAlfIFIW2FgqeM9eLyvEaYX66vr6DJMK+4U6LV0KhNw==",
+      "license": "MIT"
+    },
+    "node_modules/@rollup/rollup-android-arm-eabi": {
+      "version": "4.63.3",
+      "resolved": "https://registry.npmjs.org/@rollup/rollup-android-arm-eabi/-/rollup-android-arm-eabi-4.63.3.tgz",
+      "integrity": "sha512-w3Jnvi1ocaVm/c7yVPpfB98XeSRBMyzp6njL5MVVbGyXjpmUkN+s6Hp4t0PqhGCCaI1ZHMKXt/w0lA1RCaLVcw==",
+      "cpu": [
+        "arm"
+      ],
+      "dev": true,
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "android"
+      ]
+    },
+    "node_modules/@rollup/rollup-android-arm64": {
+      "version": "4.63.3",
+      "resolved": "https://registry.npmjs.org/@rollup/rollup-android-arm64/-/rollup-android-arm64-4.63.3.tgz",
+      "integrity": "sha512-uI/ESiaIbbRYAEhzy8PCUWDp1hB0bjAqM06mW9flOoNO4Q8DQpeoREhBR5Hegfl+wpXiguyJv6XSPzEN7OxyHQ==",
+      "cpu": [
+        "arm64"
+      ],
+      "dev": true,
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "android"
+      ]
+    },
+    "node_modules/@rollup/rollup-darwin-arm64": {
+      "version": "4.63.3",
+      "resolved": "https://registry.npmjs.org/@rollup/rollup-darwin-arm64/-/rollup-darwin-arm64-4.63.3.tgz",
+      "integrity": "sha512-oxhrd1jmXLwWZ83eQYDXxuqRdkqkzrjR3JobKeuUyfdNZo11FuQIvqEOZhyIT7OBHxXoGslDDjN0cQcM6T0TqQ==",
+      "cpu": [
+        "arm64"
+      ],
+      "dev": true,
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "darwin"
+      ]
+    },
+    "node_modules/@rollup/rollup-darwin-x64": {
+      "version": "4.63.3",
+      "resolved": "https://registry.npmjs.org/@rollup/rollup-darwin-x64/-/rollup-darwin-x64-4.63.3.tgz",
+      "integrity": "sha512-7/YiIMghVE8DrxKvNdorAaJVdriOFgOIpdStnPx8ppx5zfTwC3jBCSEAIzB7JD5404m65THl6H93UTTVUvypmg==",
+      "cpu": [
+        "x64"
+      ],
+      "dev": true,
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "darwin"
+      ]
+    },
+    "node_modules/@rollup/rollup-freebsd-arm64": {
+      "version": "4.63.3",
+      "resolved": "https://registry.npmjs.org/@rollup/rollup-freebsd-arm64/-/rollup-freebsd-arm64-4.63.3.tgz",
+      "integrity": "sha512-GXFZRRoMAytaI5z6N3Zhfw0WL18Q0M8r95D5hlC4GqE/lGk8pbSJNUBoOWDfbm6dTciqHj2nU87tI5f6XhQiOg==",
+      "cpu": [
+        "arm64"
+      ],
+      "dev": true,
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "freebsd"
+      ]
+    },
+    "node_modules/@rollup/rollup-freebsd-x64": {
+      "version": "4.63.3",
+      "resolved": "https://registry.npmjs.org/@rollup/rollup-freebsd-x64/-/rollup-freebsd-x64-4.63.3.tgz",
+      "integrity": "sha512-77W+8X3ddYgPxUpB8nZFQs2Mq+wc4HVlcSRtApXLjYBcnPMkttrSnU8VwKQjeWYhMsITHFs5cWBQ8vz1Q+5RHQ==",
+      "cpu": [
+        "x64"
+      ],
+      "dev": true,
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "freebsd"
+      ]
+    },
+    "node_modules/@rollup/rollup-linux-arm-gnueabihf": {
+      "version": "4.63.3",
+      "resolved": "https://registry.npmjs.org/@rollup/rollup-linux-arm-gnueabihf/-/rollup-linux-arm-gnueabihf-4.63.3.tgz",
+      "integrity": "sha512-FVkwK+iUC+mq+GipVK46rRVticfAPtvPUNlqlGXUDxdVk/UGjQiiiUVPUrEXdSpU2ufU0XxLGyTqDtBidDOVmg==",
+      "cpu": [
+        "arm"
+      ],
+      "dev": true,
+      "libc": [
+        "glibc"
+      ],
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "linux"
+      ]
+    },
+    "node_modules/@rollup/rollup-linux-arm-musleabihf": {
+      "version": "4.63.3",
+      "resolved": "https://registry.npmjs.org/@rollup/rollup-linux-arm-musleabihf/-/rollup-linux-arm-musleabihf-4.63.3.tgz",
+      "integrity": "sha512-+aGU1t3398yQOVj1Bz8o3e+KtswxAPvO+mtxtNdfXYMkXIHu7XhhkCD7/DEH9q8tF8uhDnMWvfpUKI8y1sZJsg==",
+      "cpu": [
+        "arm"
+      ],
+      "dev": true,
+      "libc": [
+        "musl"
+      ],
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "linux"
+      ]
+    },
+    "node_modules/@rollup/rollup-linux-arm64-gnu": {
+      "version": "4.63.3",
+      "resolved": "https://registry.npmjs.org/@rollup/rollup-linux-arm64-gnu/-/rollup-linux-arm64-gnu-4.63.3.tgz",
+      "integrity": "sha512-cR0kjpRXR2KJ2oQK8E2KTPtphs+b9hZ8IhTZubNryt/RsqgdOZBQ2Zq0q5UedtiIi0rs3jVhJh55RE1ZHUVGUA==",
+      "cpu": [
+        "arm64"
+      ],
+      "dev": true,
+      "libc": [
+        "glibc"
+      ],
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "linux"
+      ]
+    },
+    "node_modules/@rollup/rollup-linux-arm64-musl": {
+      "version": "4.63.3",
+      "resolved": "https://registry.npmjs.org/@rollup/rollup-linux-arm64-musl/-/rollup-linux-arm64-musl-4.63.3.tgz",
+      "integrity": "sha512-y1RYi4Q3/9ByVWSSt9kX2ustE0B7kFYbJ6zZdVZVyqopZs3yhCTwRfrjIX4vezUJInma/Gs6BOFDJg7yZmJ0IQ==",
+      "cpu": [
+        "arm64"
+      ],
+      "dev": true,
+      "libc": [
+        "musl"
+      ],
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "linux"
       ]
     },
     "node_modules/@rollup/rollup-linux-loong64-gnu": {
       "version": "4.63.3",
       "resolved": "https://registry.npmjs.org/@rollup/rollup-linux-loong64-gnu/-/rollup-linux-loong64-gnu-4.63.3.tgz",
       "integrity": "sha512-DNhEA5viIj3Z5bZLE4z4oV8N5ozWqDwyt7T6KG7VdLDJ0nW+rNOYlphBl4/3HQkK75qipPLsVOfStHHOwN9WSg==",
       "cpu": [
         "loong64"
       ],
       "dev": true,
@@ -1553,90 +3146,384 @@
         "openbsd"
       ]
     },
     "node_modules/@rollup/rollup-openharmony-arm64": {
       "version": "4.63.3",
       "resolved": "https://registry.npmjs.org/@rollup/rollup-openharmony-arm64/-/rollup-openharmony-arm64-4.63.3.tgz",
       "integrity": "sha512-d+CaftKgmkFBzCwezMqqy1d0QNNYugqLCMcYVQWBy5SS2YfeMP8Q8ripkgx9O8IyBXXLHrJ+aaCV4U96usv6Yg==",
       "cpu": [
         "arm64"
       ],
-      "dev": true,
+      "dev": true,
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "openharmony"
+      ]
+    },
+    "node_modules/@rollup/rollup-win32-arm64-msvc": {
+      "version": "4.63.3",
+      "resolved": "https://registry.npmjs.org/@rollup/rollup-win32-arm64-msvc/-/rollup-win32-arm64-msvc-4.63.3.tgz",
+      "integrity": "sha512-xXlDF6nR1eOuXbdDy5Hl5fmtY7teUDevF/k0O7IPoZe4Tpmdv+lgdE5JRsnhQtt37ql9P0VF2kAN9a0OCZdo+Q==",
+      "cpu": [
+        "arm64"
+      ],
+      "dev": true,
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "win32"
+      ]
+    },
+    "node_modules/@rollup/rollup-win32-ia32-msvc": {
+      "version": "4.63.3",
+      "resolved": "https://registry.npmjs.org/@rollup/rollup-win32-ia32-msvc/-/rollup-win32-ia32-msvc-4.63.3.tgz",
+      "integrity": "sha512-YtXAgLN+JP7Ay6qG3eWhc7IHMQPzLc8r3uvhAvlJIoCz/4Q32+Bl9Fmnywidh8v1GOIMmymjovfqY9ETAtysvA==",
+      "cpu": [
+        "ia32"
+      ],
+      "dev": true,
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "win32"
+      ]
+    },
+    "node_modules/@rollup/rollup-win32-x64-gnu": {
+      "version": "4.63.3",
+      "resolved": "https://registry.npmjs.org/@rollup/rollup-win32-x64-gnu/-/rollup-win32-x64-gnu-4.63.3.tgz",
+      "integrity": "sha512-WuWtSJRNo549vzcfZyEgfqb6zeSgn1F+UE5kQ+BCjzz0W4MGCjntUHkZVc1VRuAM7+ULaSyhiPxD1spyewFvkQ==",
+      "cpu": [
+        "x64"
+      ],
+      "dev": true,
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "win32"
+      ]
+    },
+    "node_modules/@rollup/rollup-win32-x64-msvc": {
+      "version": "4.63.3",
+      "resolved": "https://registry.npmjs.org/@rollup/rollup-win32-x64-msvc/-/rollup-win32-x64-msvc-4.63.3.tgz",
+      "integrity": "sha512-+lIKX7O0+IGe7WuhATaAMMeT7B76vfhXH/l9wLQL+nvyhbw2ohYCKIdWL56JfDu75CWt5oKRP4QFH/jkMtBquA==",
+      "cpu": [
+        "x64"
+      ],
+      "dev": true,
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "win32"
+      ]
+    },
+    "node_modules/@swc/helpers": {
+      "version": "0.5.23",
+      "resolved": "https://registry.npmjs.org/@swc/helpers/-/helpers-0.5.23.tgz",
+      "integrity": "sha512-5lSsMOTXURePglDfvuAQUqkGek9Hg2kksOYay2m0+XR++b2NWYL/4sWyuvVBIs8oKnJaxkdi9whaL/sqN13afw==",
+      "license": "Apache-2.0",
+      "dependencies": {
+        "tslib": "^2.8.0"
+      }
+    },
+    "node_modules/@tailwindcss/node": {
+      "version": "4.3.3",
+      "resolved": "https://registry.npmjs.org/@tailwindcss/node/-/node-4.3.3.tgz",
+      "integrity": "sha512-/T8IKEsf9VTU6tLjgC7+sv2mOPtQxzE2jMw7u4Tt40Tx+QSZxpzh95/H6cMKoja9XuW7iMdLJYBB0o9G1CaAgg==",
+      "license": "MIT",
+      "dependencies": {
+        "@jridgewell/remapping": "^2.3.5",
+        "enhanced-resolve": "^5.24.1",
+        "jiti": "^2.7.0",
+        "lightningcss": "1.32.0",
+        "magic-string": "^0.30.21",
+        "source-map-js": "^1.2.1",
+        "tailwindcss": "4.3.3"
+      }
+    },
+    "node_modules/@tailwindcss/oxide": {
+      "version": "4.3.3",
+      "resolved": "https://registry.npmjs.org/@tailwindcss/oxide/-/oxide-4.3.3.tgz",
+      "integrity": "sha512-krXjAikiaFSPaK/FkAQT5UTx3VormQaiZ5hBFlJZ9UFQGB/rwg1MZIhHAG9smMQRTdyJxP6Qt5MwMtdyU5FWrA==",
+      "license": "MIT",
+      "engines": {
+        "node": ">= 20"
+      },
+      "optionalDependencies": {
+        "@tailwindcss/oxide-android-arm64": "4.3.3",
+        "@tailwindcss/oxide-darwin-arm64": "4.3.3",
+        "@tailwindcss/oxide-darwin-x64": "4.3.3",
+        "@tailwindcss/oxide-freebsd-x64": "4.3.3",
+        "@tailwindcss/oxide-linux-arm-gnueabihf": "4.3.3",
+        "@tailwindcss/oxide-linux-arm64-gnu": "4.3.3",
+        "@tailwindcss/oxide-linux-arm64-musl": "4.3.3",
+        "@tailwindcss/oxide-linux-x64-gnu": "4.3.3",
+        "@tailwindcss/oxide-linux-x64-musl": "4.3.3",
+        "@tailwindcss/oxide-wasm32-wasi": "4.3.3",
+        "@tailwindcss/oxide-win32-arm64-msvc": "4.3.3",
+        "@tailwindcss/oxide-win32-x64-msvc": "4.3.3"
+      }
+    },
+    "node_modules/@tailwindcss/oxide-android-arm64": {
+      "version": "4.3.3",
+      "resolved": "https://registry.npmjs.org/@tailwindcss/oxide-android-arm64/-/oxide-android-arm64-4.3.3.tgz",
+      "integrity": "sha512-Y85A2gmPSkl5Ve5qR86GL4HT509cFqQh1aes9p3sSkyTPwt0Pppf3GkwGe4JPACcRYjgJIEhQgM6dBClnr0NYw==",
+      "cpu": [
+        "arm64"
+      ],
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "android"
+      ],
+      "engines": {
+        "node": ">= 20"
+      }
+    },
+    "node_modules/@tailwindcss/oxide-darwin-arm64": {
+      "version": "4.3.3",
+      "resolved": "https://registry.npmjs.org/@tailwindcss/oxide-darwin-arm64/-/oxide-darwin-arm64-4.3.3.tgz",
+      "integrity": "sha512-BiaWatpBcERQFDlOjRDpIVXuFK5PJez5SA4JMg6VYZdBYU+qKfV/vqjcIs+IYmtitf1xYQZTwXvU/8y4lfZUGw==",
+      "cpu": [
+        "arm64"
+      ],
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "darwin"
+      ],
+      "engines": {
+        "node": ">= 20"
+      }
+    },
+    "node_modules/@tailwindcss/oxide-darwin-x64": {
+      "version": "4.3.3",
+      "resolved": "https://registry.npmjs.org/@tailwindcss/oxide-darwin-x64/-/oxide-darwin-x64-4.3.3.tgz",
+      "integrity": "sha512-fAeUqfV5ndhxRwai8cXGzdLvul9utWOmeTkv69unv4ZXixjn61Z+p9lCWdwOwA3TYboG3BwdVuN/RDjhBRl0mw==",
+      "cpu": [
+        "x64"
+      ],
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "darwin"
+      ],
+      "engines": {
+        "node": ">= 20"
+      }
+    },
+    "node_modules/@tailwindcss/oxide-freebsd-x64": {
+      "version": "4.3.3",
+      "resolved": "https://registry.npmjs.org/@tailwindcss/oxide-freebsd-x64/-/oxide-freebsd-x64-4.3.3.tgz",
+      "integrity": "sha512-iyf5bV6+wnAlflVeEy7R25dupxTNECZN5QMI0qNT6eT+EgaGdZcKhGkr5SdoaWiLJ3spLqIY9VCeSGrwmtg4kw==",
+      "cpu": [
+        "x64"
+      ],
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "freebsd"
+      ],
+      "engines": {
+        "node": ">= 20"
+      }
+    },
+    "node_modules/@tailwindcss/oxide-linux-arm-gnueabihf": {
+      "version": "4.3.3",
+      "resolved": "https://registry.npmjs.org/@tailwindcss/oxide-linux-arm-gnueabihf/-/oxide-linux-arm-gnueabihf-4.3.3.tgz",
+      "integrity": "sha512-aAYUprJAJQWWbRrPvtjdroZ56Md+JM8pMiopS6xGEwDfLhqj+2ver2p4nU4Mb3CRqcMmNBjo8KkUgcxhkzVQGQ==",
+      "cpu": [
+        "arm"
+      ],
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "linux"
+      ],
+      "engines": {
+        "node": ">= 20"
+      }
+    },
+    "node_modules/@tailwindcss/oxide-linux-arm64-gnu": {
+      "version": "4.3.3",
+      "resolved": "https://registry.npmjs.org/@tailwindcss/oxide-linux-arm64-gnu/-/oxide-linux-arm64-gnu-4.3.3.tgz",
+      "integrity": "sha512-nDxldcEENOxZRzC2uu9jrutZdAAQtb+8WWDCSnWL1zvBk1+FN+x6MtDViPB5AJMfttVCUhehGWus3XBPgatM/w==",
+      "cpu": [
+        "arm64"
+      ],
+      "libc": [
+        "glibc"
+      ],
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "linux"
+      ],
+      "engines": {
+        "node": ">= 20"
+      }
+    },
+    "node_modules/@tailwindcss/oxide-linux-arm64-musl": {
+      "version": "4.3.3",
+      "resolved": "https://registry.npmjs.org/@tailwindcss/oxide-linux-arm64-musl/-/oxide-linux-arm64-musl-4.3.3.tgz",
+      "integrity": "sha512-Md44bD6veX/PC5iyF8cDVnw4HBIANZepRZZ7a8DQOvkfo5WUBwcp6iAuCUz23u+4SUkhJlD3eL7hNdW8ezd/kA==",
+      "cpu": [
+        "arm64"
+      ],
+      "libc": [
+        "musl"
+      ],
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "linux"
+      ],
+      "engines": {
+        "node": ">= 20"
+      }
+    },
+    "node_modules/@tailwindcss/oxide-linux-x64-gnu": {
+      "version": "4.3.3",
+      "resolved": "https://registry.npmjs.org/@tailwindcss/oxide-linux-x64-gnu/-/oxide-linux-x64-gnu-4.3.3.tgz",
+      "integrity": "sha512-tx7us1muwOKAKWao2v/GaafFeQboE6aj88vC6ziN2NCGcRm8gWUhwjzg+YdVB1e4boAtdtma4L43onunI6NS4w==",
+      "cpu": [
+        "x64"
+      ],
+      "libc": [
+        "glibc"
+      ],
       "license": "MIT",
       "optional": true,
       "os": [
-        "openharmony"
-      ]
+        "linux"
+      ],
+      "engines": {
+        "node": ">= 20"
+      }
     },
-    "node_modules/@rollup/rollup-win32-arm64-msvc": {
-      "version": "4.63.3",
-      "resolved": "https://registry.npmjs.org/@rollup/rollup-win32-arm64-msvc/-/rollup-win32-arm64-msvc-4.63.3.tgz",
-      "integrity": "sha512-xXlDF6nR1eOuXbdDy5Hl5fmtY7teUDevF/k0O7IPoZe4Tpmdv+lgdE5JRsnhQtt37ql9P0VF2kAN9a0OCZdo+Q==",
+    "node_modules/@tailwindcss/oxide-linux-x64-musl": {
+      "version": "4.3.3",
+      "resolved": "https://registry.npmjs.org/@tailwindcss/oxide-linux-x64-musl/-/oxide-linux-x64-musl-4.3.3.tgz",
+      "integrity": "sha512-SJxX60smvHgasZoBy11dX6YRjXJFovwWBoedhbQPOBzgFWBHGB+TVPWB9BxzR7TTxU8FQZAI2AyiNCMzFm8Img==",
       "cpu": [
-        "arm64"
+        "x64"
+      ],
+      "libc": [
+        "musl"
       ],
-      "dev": true,
       "license": "MIT",
       "optional": true,
       "os": [
-        "win32"
-      ]
+        "linux"
+      ],
+      "engines": {
+        "node": ">= 20"
+      }
     },
-    "node_modules/@rollup/rollup-win32-ia32-msvc": {
-      "version": "4.63.3",
-      "resolved": "https://registry.npmjs.org/@rollup/rollup-win32-ia32-msvc/-/rollup-win32-ia32-msvc-4.63.3.tgz",
-      "integrity": "sha512-YtXAgLN+JP7Ay6qG3eWhc7IHMQPzLc8r3uvhAvlJIoCz/4Q32+Bl9Fmnywidh8v1GOIMmymjovfqY9ETAtysvA==",
+    "node_modules/@tailwindcss/oxide-wasm32-wasi": {
+      "version": "4.3.3",
+      "resolved": "https://registry.npmjs.org/@tailwindcss/oxide-wasm32-wasi/-/oxide-wasm32-wasi-4.3.3.tgz",
+      "integrity": "sha512-jx1+rPhY/5Ympkktd656HBWEBLxP7dH06losBLjjf5vgCODXvi9KhtftWcMIwTFIDqBr7cRnQkdLnAG+IOlGvQ==",
+      "bundleDependencies": [
+        "@napi-rs/wasm-runtime",
+        "@emnapi/core",
+        "@emnapi/runtime",
+        "@tybys/wasm-util",
+        "@emnapi/wasi-threads",
+        "tslib"
+      ],
       "cpu": [
-        "ia32"
+        "wasm32"
       ],
-      "dev": true,
       "license": "MIT",
       "optional": true,
-      "os": [
-        "win32"
-      ]
+      "dependencies": {
+        "@emnapi/core": "^1.11.1",
+        "@emnapi/runtime": "^1.11.1",
+        "@emnapi/wasi-threads": "^1.2.2",
+        "@napi-rs/wasm-runtime": "^1.1.4",
+        "@tybys/wasm-util": "^0.10.2",
+        "tslib": "^2.8.1"
+      },
+      "engines": {
+        "node": ">=14.0.0"
+      }
     },
-    "node_modules/@rollup/rollup-win32-x64-gnu": {
-      "version": "4.63.3",
-      "resolved": "https://registry.npmjs.org/@rollup/rollup-win32-x64-gnu/-/rollup-win32-x64-gnu-4.63.3.tgz",
-      "integrity": "sha512-WuWtSJRNo549vzcfZyEgfqb6zeSgn1F+UE5kQ+BCjzz0W4MGCjntUHkZVc1VRuAM7+ULaSyhiPxD1spyewFvkQ==",
+    "node_modules/@tailwindcss/oxide-win32-arm64-msvc": {
+      "version": "4.3.3",
+      "resolved": "https://registry.npmjs.org/@tailwindcss/oxide-win32-arm64-msvc/-/oxide-win32-arm64-msvc-4.3.3.tgz",
+      "integrity": "sha512-3rc292Ca2ceK6Ulcc/bAVnTs/3nDtoPhyEKlgPv+yQJQi/JS/AMJlqzxvlDacL1nekbrcf6bTqp/jV4qgnPxNQ==",
       "cpu": [
-        "x64"
+        "arm64"
       ],
-      "dev": true,
       "license": "MIT",
       "optional": true,
       "os": [
         "win32"
-      ]
+      ],
+      "engines": {
+        "node": ">= 20"
+      }
     },
-    "node_modules/@rollup/rollup-win32-x64-msvc": {
-      "version": "4.63.3",
-      "resolved": "https://registry.npmjs.org/@rollup/rollup-win32-x64-msvc/-/rollup-win32-x64-msvc-4.63.3.tgz",
-      "integrity": "sha512-+lIKX7O0+IGe7WuhATaAMMeT7B76vfhXH/l9wLQL+nvyhbw2ohYCKIdWL56JfDu75CWt5oKRP4QFH/jkMtBquA==",
+    "node_modules/@tailwindcss/oxide-win32-x64-msvc": {
+      "version": "4.3.3",
+      "resolved": "https://registry.npmjs.org/@tailwindcss/oxide-win32-x64-msvc/-/oxide-win32-x64-msvc-4.3.3.tgz",
+      "integrity": "sha512-yJ0pwIVc/nYeGoV02WtsN8KYyLQv7kyI2wDnkezyJlGGjkd4QLwDGAwl47YpPJeuI0M0ObaXGSPjvWDPeTPggw==",
       "cpu": [
         "x64"
       ],
-      "dev": true,
       "license": "MIT",
       "optional": true,
       "os": [
         "win32"
-      ]
+      ],
+      "engines": {
+        "node": ">= 20"
+      }
     },
-    "node_modules/@swc/helpers": {
-      "version": "0.5.23",
-      "resolved": "https://registry.npmjs.org/@swc/helpers/-/helpers-0.5.23.tgz",
-      "integrity": "sha512-5lSsMOTXURePglDfvuAQUqkGek9Hg2kksOYay2m0+XR++b2NWYL/4sWyuvVBIs8oKnJaxkdi9whaL/sqN13afw==",
-      "license": "Apache-2.0",
+    "node_modules/@tailwindcss/postcss": {
+      "version": "4.3.3",
+      "resolved": "https://registry.npmjs.org/@tailwindcss/postcss/-/postcss-4.3.3.tgz",
+      "integrity": "sha512-JTSZZGQi1AyKirbLN3azmjVzef92tcX7h+iSqPdaeStyFpGpDlKvvpxeOE8njhbUanbRwr3z8DyzhICWnMtQeg==",
+      "license": "MIT",
       "dependencies": {
-        "tslib": "^2.8.0"
+        "@alloc/quick-lru": "^5.2.0",
+        "@tailwindcss/node": "4.3.3",
+        "@tailwindcss/oxide": "4.3.3",
+        "postcss": "^8.5.16",
+        "tailwindcss": "4.3.3"
+      }
+    },
+    "node_modules/@tanstack/query-core": {
+      "version": "5.103.1",
+      "resolved": "https://registry.npmjs.org/@tanstack/query-core/-/query-core-5.103.1.tgz",
+      "integrity": "sha512-rms8HqTGp6zA00dM+cUQ2eBcgzNJefuu5CAMB37i/6MiGT1zulPOytCFu2a0qjLqVR2n1jENPj9woqFQNuCzWA==",
+      "license": "MIT",
+      "funding": {
+        "type": "github",
+        "url": "https://github.com/sponsors/tannerlinsley"
+      }
+    },
+    "node_modules/@tanstack/react-query": {
+      "version": "5.103.1",
+      "resolved": "https://registry.npmjs.org/@tanstack/react-query/-/react-query-5.103.1.tgz",
+      "integrity": "sha512-rmAPPApNEK17VXRJhtE1rja53n23VV5w30C0saCgJhhX+EpdUnVqMGV/s5nnzV43X75KTHKzuoiuxSzGTBIkag==",
+      "license": "MIT",
+      "dependencies": {
+        "@tanstack/query-core": "5.103.1"
+      },
+      "funding": {
+        "type": "github",
+        "url": "https://github.com/sponsors/tannerlinsley"
+      },
+      "peerDependencies": {
+        "react": "^18 || ^19"
       }
     },
     "node_modules/@types/chai": {
       "version": "5.2.3",
       "resolved": "https://registry.npmjs.org/@types/chai/-/chai-5.2.3.tgz",
       "integrity": "sha512-Mw558oeA9fFbv65/y4mHtXDs9bPnFMZAL/jxdPFUpOHHIXX91mcgEHbS5Lahr+pwZFR8A7GQleRWeI6cGFC2UA==",
       "dev": true,
       "license": "MIT",
       "dependencies": {
         "@types/deep-eql": "*",
@@ -1664,31 +3551,31 @@
       "dev": true,
       "license": "MIT",
       "dependencies": {
         "undici-types": "~6.21.0"
       }
     },
     "node_modules/@types/react": {
       "version": "19.3.0",
       "resolved": "https://registry.npmjs.org/@types/react/-/react-19.3.0.tgz",
       "integrity": "sha512-N0rFCuH9YoxG9/m61l9MfpJKfmLOVU0em7ipIz6TRgSSkvReLB9vL85GB+yr8Bs5leqpvg96JSwF4ZS1s4viQg==",
-      "dev": true,
+      "devOptional": true,
       "license": "MIT",
       "dependencies": {
         "csstype": "^3.2.2"
       }
     },
     "node_modules/@types/react-dom": {
       "version": "19.3.0",
       "resolved": "https://registry.npmjs.org/@types/react-dom/-/react-dom-19.3.0.tgz",
       "integrity": "sha512-ZI7bU42mZXXKHn/qNLEw2IrbiINU7X5+vfgdixBHkCNpYWXjKgfQ/P+uyGb5CjOLB9UcnTeg3rylQtV2hym44Q==",
-      "dev": true,
+      "devOptional": true,
       "license": "MIT",
       "peerDependencies": {
         "@types/react": "^19.3.0"
       }
     },
     "node_modules/@vitest/expect": {
       "version": "3.2.7",
       "resolved": "https://registry.npmjs.org/@vitest/expect/-/expect-3.2.7.tgz",
       "integrity": "sha512-E8eBXaKibuvH2pSZErOjdVb5vF4PbKYcrnluBTYxEk1l/VhhwZg1kZQsdtjq+CsF5CFydf2Rdkz7jDHKSisi3w==",
       "dev": true,
@@ -1795,20 +3682,32 @@
       "license": "MIT",
       "dependencies": {
         "@vitest/pretty-format": "3.2.7",
         "loupe": "^3.1.4",
         "tinyrainbow": "^2.0.0"
       },
       "funding": {
         "url": "https://opencollective.com/vitest"
       }
     },
+    "node_modules/aria-hidden": {
+      "version": "1.2.6",
+      "resolved": "https://registry.npmjs.org/aria-hidden/-/aria-hidden-1.2.6.tgz",
+      "integrity": "sha512-ik3ZgC9dY/lYVVM++OISsaYDeg1tb0VtP5uL3ouh1koGOaUMDPpbFIei4JkFimWUFPn90sbMNMXQAIVOlnYKJA==",
+      "license": "MIT",
+      "dependencies": {
+        "tslib": "^2.0.0"
+      },
+      "engines": {
+        "node": ">=10"
+      }
+    },
     "node_modules/assertion-error": {
       "version": "2.0.1",
       "resolved": "https://registry.npmjs.org/assertion-error/-/assertion-error-2.0.1.tgz",
       "integrity": "sha512-Izi8RQcffqCeNVgFigKli1ssklIbpHnCYc6AknXGYoB6grJqyeby7jv12JUQgmTAnIDnbck1uxksT4dzN3PWBA==",
       "dev": true,
       "license": "MIT",
       "engines": {
         "node": ">=12"
       }
     },
@@ -1874,31 +3773,51 @@
     "node_modules/check-error": {
       "version": "2.1.3",
       "resolved": "https://registry.npmjs.org/check-error/-/check-error-2.1.3.tgz",
       "integrity": "sha512-PAJdDJusoxnwm1VwW07VWwUN1sl7smmC3OKggvndJFadxxDRyFJBX/ggnu/KE4kQAB7a3Dp8f/YXC1FlUprWmA==",
       "dev": true,
       "license": "MIT",
       "engines": {
         "node": ">= 16"
       }
     },
+    "node_modules/class-variance-authority": {
+      "version": "0.7.1",
+      "resolved": "https://registry.npmjs.org/class-variance-authority/-/class-variance-authority-0.7.1.tgz",
+      "integrity": "sha512-Ka+9Trutv7G8M6WT6SeiRWz792K5qEqIGEGzXKhAE6xOWAY6pPH8U+9IY3oCMv6kqTmLsv7Xh/2w2RigkePMsg==",
+      "license": "Apache-2.0",
+      "dependencies": {
+        "clsx": "^2.1.1"
+      },
+      "funding": {
+        "url": "https://polar.sh/cva"
+      }
+    },
     "node_modules/client-only": {
       "version": "0.0.1",
       "resolved": "https://registry.npmjs.org/client-only/-/client-only-0.0.1.tgz",
       "integrity": "sha512-IV3Ou0jSMzZrd3pZ48nLkT9DA7Ag1pnPzaiQhpW7c3RbcqqzvzzVu+L8gfqMp/8IM2MQtSiqaCxrrcfu8I8rMA==",
       "license": "MIT"
     },
+    "node_modules/clsx": {
+      "version": "2.1.1",
+      "resolved": "https://registry.npmjs.org/clsx/-/clsx-2.1.1.tgz",
+      "integrity": "sha512-eYm0QWBtUrBWZWG0d386OGAw16Z995PiOVo2B7bjWSbHedGl5e0ZWaq65kOGgUSNesEIDkB9ISbTg/JK9dhCZA==",
+      "license": "MIT",
+      "engines": {
+        "node": ">=6"
+      }
+    },
     "node_modules/csstype": {
       "version": "3.2.3",
       "resolved": "https://registry.npmjs.org/csstype/-/csstype-3.2.3.tgz",
       "integrity": "sha512-z1HGKcYy2xA8AGQfwrn0PAy+PB7X/GSj3UVJW9qKyn43xWa+gl5nXmU4qqLMRzWVLFC8KusUX8T/0kCiOYpAIQ==",
-      "dev": true,
       "license": "MIT"
     },
     "node_modules/debug": {
       "version": "4.4.3",
       "resolved": "https://registry.npmjs.org/debug/-/debug-4.4.3.tgz",
       "integrity": "sha512-RGwwWnwQvkVfavKVt22FGLw+xYSdzARwm0ru6DhTVA3umU5hZc28V3kO4stgYryrTlLpuvgI9GiijltAjNbcqA==",
       "dev": true,
       "license": "MIT",
       "dependencies": {
         "ms": "^2.1.3"
@@ -1920,25 +3839,43 @@
       "license": "MIT",
       "engines": {
         "node": ">=6"
       }
     },
     "node_modules/detect-libc": {
       "version": "2.1.2",
       "resolved": "https://registry.npmjs.org/detect-libc/-/detect-libc-2.1.2.tgz",
       "integrity": "sha512-Btj2BOOO83o3WyH59e8MgXsxEQVcarkUOpEYrubB0urwnN10yQ364rsiByU11nZlqWYZm05i/of7io4mzihBtQ==",
       "license": "Apache-2.0",
-      "optional": true,
       "engines": {
         "node": ">=8"
       }
     },
+    "node_modules/detect-node-es": {
+      "version": "1.1.0",
+      "resolved": "https://registry.npmjs.org/detect-node-es/-/detect-node-es-1.1.0.tgz",
+      "integrity": "sha512-ypdmJU/TbBby2Dxibuv7ZLW3Bs1QEmM7nHjEANfohJLvE0XVujisn1qPJcZxg+qDucsr+bP6fLD1rPS3AhJ7EQ==",
+      "license": "MIT"
+    },
+    "node_modules/enhanced-resolve": {
+      "version": "5.25.1",
+      "resolved": "https://registry.npmjs.org/enhanced-resolve/-/enhanced-resolve-5.25.1.tgz",
+      "integrity": "sha512-nGXts5znJzmWPu+mIE9izCOzdg63oJca2mDzGWWTth7sr4aCToKcoyFVBQwN75Ij5Pf6p510EwkTqViTRzDV+w==",
+      "license": "MIT",
+      "dependencies": {
+        "graceful-fs": "^4.2.4",
+        "tapable": "^2.3.3"
+      },
+      "engines": {
+        "node": ">=10.13.0"
+      }
+    },
     "node_modules/es-module-lexer": {
       "version": "1.7.0",
       "resolved": "https://registry.npmjs.org/es-module-lexer/-/es-module-lexer-1.7.0.tgz",
       "integrity": "sha512-jEQoCwk8hyb2AZziIOLhDqpm5+2ww5uIE6lkO/6jcOCusfk6LhMHpXXfBLXTZ7Ydyt0j4VoUQv6uGNYbdW+kBA==",
       "dev": true,
       "license": "MIT"
     },
     "node_modules/esbuild": {
       "version": "0.28.2",
       "resolved": "https://registry.npmjs.org/esbuild/-/esbuild-0.28.2.tgz",
@@ -1974,101 +3911,403 @@
         "@esbuild/netbsd-x64": "0.28.2",
         "@esbuild/openbsd-arm64": "0.28.2",
         "@esbuild/openbsd-x64": "0.28.2",
         "@esbuild/openharmony-arm64": "0.28.2",
         "@esbuild/sunos-x64": "0.28.2",
         "@esbuild/win32-arm64": "0.28.2",
         "@esbuild/win32-ia32": "0.28.2",
         "@esbuild/win32-x64": "0.28.2"
       }
     },
-    "node_modules/estree-walker": {
-      "version": "3.0.3",
-      "resolved": "https://registry.npmjs.org/estree-walker/-/estree-walker-3.0.3.tgz",
-      "integrity": "sha512-7RUKfXgSMMkzt6ZuXmqapOurLGPPfgj6l9uRZ7lRGolvk0y2yocc35LdcxKC5PQZdn2DMqioAQ2NoWcrTKmm6g==",
-      "dev": true,
-      "license": "MIT",
-      "dependencies": {
-        "@types/estree": "^1.0.0"
+    "node_modules/estree-walker": {
+      "version": "3.0.3",
+      "resolved": "https://registry.npmjs.org/estree-walker/-/estree-walker-3.0.3.tgz",
+      "integrity": "sha512-7RUKfXgSMMkzt6ZuXmqapOurLGPPfgj6l9uRZ7lRGolvk0y2yocc35LdcxKC5PQZdn2DMqioAQ2NoWcrTKmm6g==",
+      "dev": true,
+      "license": "MIT",
+      "dependencies": {
+        "@types/estree": "^1.0.0"
+      }
+    },
+    "node_modules/expect-type": {
+      "version": "1.4.0",
+      "resolved": "https://registry.npmjs.org/expect-type/-/expect-type-1.4.0.tgz",
+      "integrity": "sha512-KfYbmpRm0VbLjEvVa9yGwCi9GI34xvi7A/HXYWQO65CSD2u3MczUJSuwXKFIxlGsgBQizV9q5J9NHj4VG0n+pA==",
+      "dev": true,
+      "license": "Apache-2.0",
+      "engines": {
+        "node": ">=12.0.0"
+      }
+    },
+    "node_modules/fdir": {
+      "version": "6.5.0",
+      "resolved": "https://registry.npmjs.org/fdir/-/fdir-6.5.0.tgz",
+      "integrity": "sha512-tIbYtZbucOs0BRGqPJkshJUYdL+SDH7dVM8gjy+ERp3WAUjLEFJE+02kanyHtwjWOnwrKYBiwAmM0p4kLJAnXg==",
+      "dev": true,
+      "license": "MIT",
+      "engines": {
+        "node": ">=12.0.0"
+      },
+      "peerDependencies": {
+        "picomatch": "^3 || ^4"
+      },
+      "peerDependenciesMeta": {
+        "picomatch": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/fsevents": {
+      "version": "2.3.3",
+      "resolved": "https://registry.npmjs.org/fsevents/-/fsevents-2.3.3.tgz",
+      "integrity": "sha512-5xoDfX+fL7faATnagmWPpbFtwh/R77WmMMqqHGS65C3vvB0YHrgF+B1YmZ3441tMj5n63k0212XNoJwzlhffQw==",
+      "dev": true,
+      "hasInstallScript": true,
+      "license": "MIT",
+      "optional": true,
+      "os": [
+        "darwin"
+      ],
+      "engines": {
+        "node": "^8.16.0 || ^10.6.0 || >=11.0.0"
+      }
+    },
+    "node_modules/get-nonce": {
+      "version": "1.0.1",
+      "resolved": "https://registry.npmjs.org/get-nonce/-/get-nonce-1.0.1.tgz",
+      "integrity": "sha512-FJhYRoDaiatfEkUK8HKlicmu/3SGFD51q3itKDGoSTysQJBnfOcxU5GxnhE1E6soB76MbT0MBtnKJuXyAx+96Q==",
+      "license": "MIT",
+      "engines": {
+        "node": ">=6"
+      }
+    },
+    "node_modules/goober": {
+      "version": "2.1.19",
+      "resolved": "https://registry.npmjs.org/goober/-/goober-2.1.19.tgz",
+      "integrity": "sha512-U7veizMqxyKlM58+Z5j2ngJBH/r9siDmxpvNxSw0PylF6WQvrASJEZrxh1hidRBJc2jqoBVSyOban5u8m+6Rxg==",
+      "license": "MIT",
+      "peerDependencies": {
+        "csstype": "^3.0.10"
+      }
+    },
+    "node_modules/graceful-fs": {
+      "version": "4.2.11",
+      "resolved": "https://registry.npmjs.org/graceful-fs/-/graceful-fs-4.2.11.tgz",
+      "integrity": "sha512-RbJ5/jmFcNNCcDV5o9eTnBLJ/HszWV0P73bc+Ff4nS/rJj+YaS6IGyiOL0VoBYX+l1Wrl3k63h/KrH+nhJ0XvQ==",
+      "license": "ISC"
+    },
+    "node_modules/jiti": {
+      "version": "2.7.0",
+      "resolved": "https://registry.npmjs.org/jiti/-/jiti-2.7.0.tgz",
+      "integrity": "sha512-AC/7JofJvZGrrneWNaEnJeOLUx+JlGt7tNa0wZiRPT4MY1wmfKjt2+6O2p2uz2+skll8OZZmJMNqeke7kKbNgQ==",
+      "license": "MIT",
+      "bin": {
+        "jiti": "lib/jiti-cli.mjs"
+      }
+    },
+    "node_modules/jose": {
+      "version": "6.2.12",
+      "resolved": "https://registry.npmjs.org/jose/-/jose-6.2.12.tgz",
+      "integrity": "sha512-9NiFmJEex0sy2Dk58j2UGBSHgUs2ypF9eZSu4L6vjOX3Dp96Sw1F3uL+H+D1sx02jZZdzUT0HgvCy59CuvXcWw==",
+      "license": "MIT",
+      "funding": {
+        "url": "https://github.com/sponsors/panva"
+      }
+    },
+    "node_modules/js-tokens": {
+      "version": "9.0.1",
+      "resolved": "https://registry.npmjs.org/js-tokens/-/js-tokens-9.0.1.tgz",
+      "integrity": "sha512-mxa9E9ITFOt0ban3j6L5MpjwegGz6lBQmM1IJkWeBZGcMxto50+eWdjC/52xDbS2vy0k7vIMK0Fe2wfL9OQSpQ==",
+      "dev": true,
+      "license": "MIT"
+    },
+    "node_modules/lightningcss": {
+      "version": "1.32.0",
+      "resolved": "https://registry.npmjs.org/lightningcss/-/lightningcss-1.32.0.tgz",
+      "integrity": "sha512-NXYBzinNrblfraPGyrbPoD19C1h9lfI/1mzgWYvXUTe414Gz/X1FD2XBZSZM7rRTrMA8JL3OtAaGifrIKhQ5yQ==",
+      "license": "MPL-2.0",
+      "dependencies": {
+        "detect-libc": "^2.0.3"
+      },
+      "engines": {
+        "node": ">= 12.0.0"
+      },
+      "funding": {
+        "type": "opencollective",
+        "url": "https://opencollective.com/parcel"
+      },
+      "optionalDependencies": {
+        "lightningcss-android-arm64": "1.32.0",
+        "lightningcss-darwin-arm64": "1.32.0",
+        "lightningcss-darwin-x64": "1.32.0",
+        "lightningcss-freebsd-x64": "1.32.0",
+        "lightningcss-linux-arm-gnueabihf": "1.32.0",
+        "lightningcss-linux-arm64-gnu": "1.32.0",
+        "lightningcss-linux-arm64-musl": "1.32.0",
+        "lightningcss-linux-x64-gnu": "1.32.0",
+        "lightningcss-linux-x64-musl": "1.32.0",
+        "lightningcss-win32-arm64-msvc": "1.32.0",
+        "lightningcss-win32-x64-msvc": "1.32.0"
+      }
+    },
+    "node_modules/lightningcss-android-arm64": {
+      "version": "1.32.0",
+      "resolved": "https://registry.npmjs.org/lightningcss-android-arm64/-/lightningcss-android-arm64-1.32.0.tgz",
+      "integrity": "sha512-YK7/ClTt4kAK0vo6w3X+Pnm0D2cf2vPHbhOXdoNti1Ga0al1P4TBZhwjATvjNwLEBCnKvjJc2jQgHXH0NEwlAg==",
+      "cpu": [
+        "arm64"
+      ],
+      "license": "MPL-2.0",
+      "optional": true,
+      "os": [
+        "android"
+      ],
+      "engines": {
+        "node": ">= 12.0.0"
+      },
+      "funding": {
+        "type": "opencollective",
+        "url": "https://opencollective.com/parcel"
+      }
+    },
+    "node_modules/lightningcss-darwin-arm64": {
+      "version": "1.32.0",
+      "resolved": "https://registry.npmjs.org/lightningcss-darwin-arm64/-/lightningcss-darwin-arm64-1.32.0.tgz",
+      "integrity": "sha512-RzeG9Ju5bag2Bv1/lwlVJvBE3q6TtXskdZLLCyfg5pt+HLz9BqlICO7LZM7VHNTTn/5PRhHFBSjk5lc4cmscPQ==",
+      "cpu": [
+        "arm64"
+      ],
+      "license": "MPL-2.0",
+      "optional": true,
+      "os": [
+        "darwin"
+      ],
+      "engines": {
+        "node": ">= 12.0.0"
+      },
+      "funding": {
+        "type": "opencollective",
+        "url": "https://opencollective.com/parcel"
+      }
+    },
+    "node_modules/lightningcss-darwin-x64": {
+      "version": "1.32.0",
+      "resolved": "https://registry.npmjs.org/lightningcss-darwin-x64/-/lightningcss-darwin-x64-1.32.0.tgz",
+      "integrity": "sha512-U+QsBp2m/s2wqpUYT/6wnlagdZbtZdndSmut/NJqlCcMLTWp5muCrID+K5UJ6jqD2BFshejCYXniPDbNh73V8w==",
+      "cpu": [
+        "x64"
+      ],
+      "license": "MPL-2.0",
+      "optional": true,
+      "os": [
+        "darwin"
+      ],
+      "engines": {
+        "node": ">= 12.0.0"
+      },
+      "funding": {
+        "type": "opencollective",
+        "url": "https://opencollective.com/parcel"
+      }
+    },
+    "node_modules/lightningcss-freebsd-x64": {
+      "version": "1.32.0",
+      "resolved": "https://registry.npmjs.org/lightningcss-freebsd-x64/-/lightningcss-freebsd-x64-1.32.0.tgz",
+      "integrity": "sha512-JCTigedEksZk3tHTTthnMdVfGf61Fky8Ji2E4YjUTEQX14xiy/lTzXnu1vwiZe3bYe0q+SpsSH/CTeDXK6WHig==",
+      "cpu": [
+        "x64"
+      ],
+      "license": "MPL-2.0",
+      "optional": true,
+      "os": [
+        "freebsd"
+      ],
+      "engines": {
+        "node": ">= 12.0.0"
+      },
+      "funding": {
+        "type": "opencollective",
+        "url": "https://opencollective.com/parcel"
+      }
+    },
+    "node_modules/lightningcss-linux-arm-gnueabihf": {
+      "version": "1.32.0",
+      "resolved": "https://registry.npmjs.org/lightningcss-linux-arm-gnueabihf/-/lightningcss-linux-arm-gnueabihf-1.32.0.tgz",
+      "integrity": "sha512-x6rnnpRa2GL0zQOkt6rts3YDPzduLpWvwAF6EMhXFVZXD4tPrBkEFqzGowzCsIWsPjqSK+tyNEODUBXeeVHSkw==",
+      "cpu": [
+        "arm"
+      ],
+      "license": "MPL-2.0",
+      "optional": true,
+      "os": [
+        "linux"
+      ],
+      "engines": {
+        "node": ">= 12.0.0"
+      },
+      "funding": {
+        "type": "opencollective",
+        "url": "https://opencollective.com/parcel"
       }
     },
-    "node_modules/expect-type": {
-      "version": "1.4.0",
-      "resolved": "https://registry.npmjs.org/expect-type/-/expect-type-1.4.0.tgz",
-      "integrity": "sha512-KfYbmpRm0VbLjEvVa9yGwCi9GI34xvi7A/HXYWQO65CSD2u3MczUJSuwXKFIxlGsgBQizV9q5J9NHj4VG0n+pA==",
-      "dev": true,
-      "license": "Apache-2.0",
+    "node_modules/lightningcss-linux-arm64-gnu": {
+      "version": "1.32.0",
+      "resolved": "https://registry.npmjs.org/lightningcss-linux-arm64-gnu/-/lightningcss-linux-arm64-gnu-1.32.0.tgz",
+      "integrity": "sha512-0nnMyoyOLRJXfbMOilaSRcLH3Jw5z9HDNGfT/gwCPgaDjnx0i8w7vBzFLFR1f6CMLKF8gVbebmkUN3fa/kQJpQ==",
+      "cpu": [
+        "arm64"
+      ],
+      "libc": [
+        "glibc"
+      ],
+      "license": "MPL-2.0",
+      "optional": true,
+      "os": [
+        "linux"
+      ],
       "engines": {
-        "node": ">=12.0.0"
+        "node": ">= 12.0.0"
+      },
+      "funding": {
+        "type": "opencollective",
+        "url": "https://opencollective.com/parcel"
       }
     },
-    "node_modules/fdir": {
-      "version": "6.5.0",
-      "resolved": "https://registry.npmjs.org/fdir/-/fdir-6.5.0.tgz",
-      "integrity": "sha512-tIbYtZbucOs0BRGqPJkshJUYdL+SDH7dVM8gjy+ERp3WAUjLEFJE+02kanyHtwjWOnwrKYBiwAmM0p4kLJAnXg==",
-      "dev": true,
-      "license": "MIT",
+    "node_modules/lightningcss-linux-arm64-musl": {
+      "version": "1.32.0",
+      "resolved": "https://registry.npmjs.org/lightningcss-linux-arm64-musl/-/lightningcss-linux-arm64-musl-1.32.0.tgz",
+      "integrity": "sha512-UpQkoenr4UJEzgVIYpI80lDFvRmPVg6oqboNHfoH4CQIfNA+HOrZ7Mo7KZP02dC6LjghPQJeBsvXhJod/wnIBg==",
+      "cpu": [
+        "arm64"
+      ],
+      "libc": [
+        "musl"
+      ],
+      "license": "MPL-2.0",
+      "optional": true,
+      "os": [
+        "linux"
+      ],
       "engines": {
-        "node": ">=12.0.0"
+        "node": ">= 12.0.0"
       },
-      "peerDependencies": {
-        "picomatch": "^3 || ^4"
+      "funding": {
+        "type": "opencollective",
+        "url": "https://opencollective.com/parcel"
+      }
+    },
+    "node_modules/lightningcss-linux-x64-gnu": {
+      "version": "1.32.0",
+      "resolved": "https://registry.npmjs.org/lightningcss-linux-x64-gnu/-/lightningcss-linux-x64-gnu-1.32.0.tgz",
+      "integrity": "sha512-V7Qr52IhZmdKPVr+Vtw8o+WLsQJYCTd8loIfpDaMRWGUZfBOYEJeyJIkqGIDMZPwPx24pUMfwSxxI8phr/MbOA==",
+      "cpu": [
+        "x64"
+      ],
+      "libc": [
+        "glibc"
+      ],
+      "license": "MPL-2.0",
+      "optional": true,
+      "os": [
+        "linux"
+      ],
+      "engines": {
+        "node": ">= 12.0.0"
       },
-      "peerDependenciesMeta": {
-        "picomatch": {
-          "optional": true
-        }
+      "funding": {
+        "type": "opencollective",
+        "url": "https://opencollective.com/parcel"
       }
     },
-    "node_modules/fsevents": {
-      "version": "2.3.3",
-      "resolved": "https://registry.npmjs.org/fsevents/-/fsevents-2.3.3.tgz",
-      "integrity": "sha512-5xoDfX+fL7faATnagmWPpbFtwh/R77WmMMqqHGS65C3vvB0YHrgF+B1YmZ3441tMj5n63k0212XNoJwzlhffQw==",
-      "dev": true,
-      "hasInstallScript": true,
-      "license": "MIT",
+    "node_modules/lightningcss-linux-x64-musl": {
+      "version": "1.32.0",
+      "resolved": "https://registry.npmjs.org/lightningcss-linux-x64-musl/-/lightningcss-linux-x64-musl-1.32.0.tgz",
+      "integrity": "sha512-bYcLp+Vb0awsiXg/80uCRezCYHNg1/l3mt0gzHnWV9XP1W5sKa5/TCdGWaR/zBM2PeF/HbsQv/j2URNOiVuxWg==",
+      "cpu": [
+        "x64"
+      ],
+      "libc": [
+        "musl"
+      ],
+      "license": "MPL-2.0",
       "optional": true,
       "os": [
-        "darwin"
+        "linux"
       ],
       "engines": {
-        "node": "^8.16.0 || ^10.6.0 || >=11.0.0"
+        "node": ">= 12.0.0"
+      },
+      "funding": {
+        "type": "opencollective",
+        "url": "https://opencollective.com/parcel"
       }
     },
-    "node_modules/jose": {
-      "version": "6.2.12",
-      "resolved": "https://registry.npmjs.org/jose/-/jose-6.2.12.tgz",
-      "integrity": "sha512-9NiFmJEex0sy2Dk58j2UGBSHgUs2ypF9eZSu4L6vjOX3Dp96Sw1F3uL+H+D1sx02jZZdzUT0HgvCy59CuvXcWw==",
-      "license": "MIT",
+    "node_modules/lightningcss-win32-arm64-msvc": {
+      "version": "1.32.0",
+      "resolved": "https://registry.npmjs.org/lightningcss-win32-arm64-msvc/-/lightningcss-win32-arm64-msvc-1.32.0.tgz",
+      "integrity": "sha512-8SbC8BR40pS6baCM8sbtYDSwEVQd4JlFTOlaD3gWGHfThTcABnNDBda6eTZeqbofalIJhFx0qKzgHJmcPTnGdw==",
+      "cpu": [
+        "arm64"
+      ],
+      "license": "MPL-2.0",
+      "optional": true,
+      "os": [
+        "win32"
+      ],
+      "engines": {
+        "node": ">= 12.0.0"
+      },
       "funding": {
-        "url": "https://github.com/sponsors/panva"
+        "type": "opencollective",
+        "url": "https://opencollective.com/parcel"
       }
     },
-    "node_modules/js-tokens": {
-      "version": "9.0.1",
-      "resolved": "https://registry.npmjs.org/js-tokens/-/js-tokens-9.0.1.tgz",
-      "integrity": "sha512-mxa9E9ITFOt0ban3j6L5MpjwegGz6lBQmM1IJkWeBZGcMxto50+eWdjC/52xDbS2vy0k7vIMK0Fe2wfL9OQSpQ==",
-      "dev": true,
-      "license": "MIT"
+    "node_modules/lightningcss-win32-x64-msvc": {
+      "version": "1.32.0",
+      "resolved": "https://registry.npmjs.org/lightningcss-win32-x64-msvc/-/lightningcss-win32-x64-msvc-1.32.0.tgz",
+      "integrity": "sha512-Amq9B/SoZYdDi1kFrojnoqPLxYhQ4Wo5XiL8EVJrVsB8ARoC1PWW6VGtT0WKCemjy8aC+louJnjS7U18x3b06Q==",
+      "cpu": [
+        "x64"
+      ],
+      "license": "MPL-2.0",
+      "optional": true,
+      "os": [
+        "win32"
+      ],
+      "engines": {
+        "node": ">= 12.0.0"
+      },
+      "funding": {
+        "type": "opencollective",
+        "url": "https://opencollective.com/parcel"
+      }
     },
     "node_modules/loupe": {
       "version": "3.2.1",
       "resolved": "https://registry.npmjs.org/loupe/-/loupe-3.2.1.tgz",
       "integrity": "sha512-CdzqowRJCeLU72bHvWqwRBBlLcMEtIvGrlvef74kMnV2AolS9Y8xUv1I0U/MNAWMhBlKIoyuEgoJ0t/bbwHbLQ==",
       "dev": true,
       "license": "MIT"
     },
+    "node_modules/lucide-react": {
+      "version": "1.47.0",
+      "resolved": "https://registry.npmjs.org/lucide-react/-/lucide-react-1.47.0.tgz",
+      "integrity": "sha512-o8C23aXpNQypRY73W7fW02EyvWJinEMXgKeGjFoKym0zj3Q73hD97A1IQps1g8C14HTGsOpZL0Am+xjfgFZAbg==",
+      "license": "ISC",
+      "peerDependencies": {
+        "react": "^16.5.1 || ^17.0.0 || ^18.0.0 || ^19.0.0"
+      }
+    },
     "node_modules/magic-string": {
       "version": "0.30.21",
       "resolved": "https://registry.npmjs.org/magic-string/-/magic-string-0.30.21.tgz",
       "integrity": "sha512-vd2F4YUyEXKGcLHoq+TEyCjxueSeHnFxyyjNp80yg0XV4vUhnDer/lvvlqM/arB5bXQN5K2/3oinyCRyx8T2CQ==",
-      "dev": true,
       "license": "MIT",
       "dependencies": {
         "@jridgewell/sourcemap-codec": "^1.5.5"
       }
     },
     "node_modules/ms": {
       "version": "2.1.3",
       "resolved": "https://registry.npmjs.org/ms/-/ms-2.1.3.tgz",
       "integrity": "sha512-6FlzubTLZG3J2a/NVCAleEhjzq5oxgHyaCU9yYXvcLsvoVaHJq/s5xXI6/XXP6tz7R9xAOtHnSO/tXtF3WRTlA==",
       "dev": true,
@@ -2257,20 +4496,97 @@
     },
     "node_modules/preact-render-to-string": {
       "version": "6.5.11",
       "resolved": "https://registry.npmjs.org/preact-render-to-string/-/preact-render-to-string-6.5.11.tgz",
       "integrity": "sha512-ubnauqoGczeGISiOh6RjX0/cdaF8v/oDXIjO85XALCQjwQP+SB4RDXXtvZ6yTYSjG+PC1QRP2AhPgCEsM2EvUw==",
       "license": "MIT",
       "peerDependencies": {
         "preact": ">=10"
       }
     },
+    "node_modules/radix-ui": {
+      "version": "1.6.7",
+      "resolved": "https://registry.npmjs.org/radix-ui/-/radix-ui-1.6.7.tgz",
+      "integrity": "sha512-QBdhh1arIEUvPC0dQ5+nwWAxt7+N+oP/9jPwjJkGFoSk/sqxg32gJtSXGtFh8frAIcS6oC9cx2Q+7KYCQLOAeA==",
+      "license": "MIT",
+      "dependencies": {
+        "@radix-ui/primitive": "1.1.7",
+        "@radix-ui/react-accessible-icon": "1.1.15",
+        "@radix-ui/react-accordion": "1.2.20",
+        "@radix-ui/react-alert-dialog": "1.1.23",
+        "@radix-ui/react-arrow": "1.1.15",
+        "@radix-ui/react-aspect-ratio": "1.1.15",
+        "@radix-ui/react-avatar": "1.2.6",
+        "@radix-ui/react-checkbox": "1.3.11",
+        "@radix-ui/react-collapsible": "1.1.20",
+        "@radix-ui/react-collection": "1.1.15",
+        "@radix-ui/react-compose-refs": "1.1.5",
+        "@radix-ui/react-context": "1.2.2",
+        "@radix-ui/react-context-menu": "2.3.7",
+        "@radix-ui/react-dialog": "1.1.23",
+        "@radix-ui/react-direction": "1.1.4",
+        "@radix-ui/react-dismissable-layer": "1.1.19",
+        "@radix-ui/react-dropdown-menu": "2.1.24",
+        "@radix-ui/react-focus-guards": "1.1.6",
+        "@radix-ui/react-focus-scope": "1.1.16",
+        "@radix-ui/react-form": "0.1.16",
+        "@radix-ui/react-hover-card": "1.1.23",
+        "@radix-ui/react-label": "2.1.15",
+        "@radix-ui/react-menu": "2.1.24",
+        "@radix-ui/react-menubar": "1.1.24",
+        "@radix-ui/react-navigation-menu": "1.2.22",
+        "@radix-ui/react-one-time-password-field": "0.1.16",
+        "@radix-ui/react-password-toggle-field": "0.1.11",
+        "@radix-ui/react-popover": "1.1.23",
+        "@radix-ui/react-popper": "1.3.7",
+        "@radix-ui/react-portal": "1.1.17",
+        "@radix-ui/react-presence": "1.1.10",
+        "@radix-ui/react-primitive": "2.1.10",
+        "@radix-ui/react-progress": "1.1.16",
+        "@radix-ui/react-radio-group": "1.4.7",
+        "@radix-ui/react-roving-focus": "1.1.19",
+        "@radix-ui/react-scroll-area": "1.2.18",
+        "@radix-ui/react-select": "2.3.7",
+        "@radix-ui/react-separator": "1.1.15",
+        "@radix-ui/react-slider": "1.4.7",
+        "@radix-ui/react-slot": "1.3.3",
+        "@radix-ui/react-switch": "1.3.7",
+        "@radix-ui/react-tabs": "1.1.21",
+        "@radix-ui/react-toast": "1.2.23",
+        "@radix-ui/react-toggle": "1.1.18",
+        "@radix-ui/react-toggle-group": "1.1.19",
+        "@radix-ui/react-toolbar": "1.1.19",
+        "@radix-ui/react-tooltip": "1.2.16",
+        "@radix-ui/react-use-callback-ref": "1.1.4",
+        "@radix-ui/react-use-controllable-state": "1.2.6",
+        "@radix-ui/react-use-effect-event": "0.0.5",
+        "@radix-ui/react-use-escape-keydown": "1.1.5",
+        "@radix-ui/react-use-is-hydrated": "0.1.3",
+        "@radix-ui/react-use-layout-effect": "1.1.4",
+        "@radix-ui/react-use-size": "1.1.4",
+        "@radix-ui/react-visually-hidden": "1.2.11"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc",
+        "react-dom": "^16.8 || ^17.0 || ^18.0 || ^19.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
     "node_modules/react": {
       "version": "19.3.0",
       "resolved": "https://registry.npmjs.org/react/-/react-19.3.0.tgz",
       "integrity": "sha512-E8LUcbtBWt20bbl2YoHfx4ZDBdxVTfOKtCZn9cDSJ4l6/nuoApcpIBcj47t2wZoVX8g2ZHuMHbiShgCR1T5Sog==",
       "license": "MIT",
       "engines": {
         "node": ">=0.10.0"
       }
     },
     "node_modules/react-dom": {
@@ -2278,20 +4594,116 @@
       "resolved": "https://registry.npmjs.org/react-dom/-/react-dom-19.3.0.tgz",
       "integrity": "sha512-JDk8dgif51OjFoDE70+OT9ICyYr+69HlmihNwp1+Nsfbna3t5sIiCa9ZJktDmQ4/1b/rn26hIAR2uYXDMr5r0Q==",
       "license": "MIT",
       "dependencies": {
         "scheduler": "^0.28.0"
       },
       "peerDependencies": {
         "react": "^19.3.0"
       }
     },
+    "node_modules/react-hot-toast": {
+      "version": "2.6.1",
+      "resolved": "https://registry.npmjs.org/react-hot-toast/-/react-hot-toast-2.6.1.tgz",
+      "integrity": "sha512-gjFsf05dxcku0FkyMtFH+XmYFdA2odXfBVknEr/IZ+XpUjwsgvtY2616sOpUynK+l9ykQDRNpaJ1hYfuximUiA==",
+      "license": "MIT",
+      "dependencies": {
+        "csstype": "^3.1.3",
+        "goober": "^2.1.16"
+      },
+      "engines": {
+        "node": ">=10"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "@types/react-dom": "*",
+        "react": ">=16",
+        "react-dom": ">=16"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        },
+        "@types/react-dom": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/react-remove-scroll": {
+      "version": "2.7.2",
+      "resolved": "https://registry.npmjs.org/react-remove-scroll/-/react-remove-scroll-2.7.2.tgz",
+      "integrity": "sha512-Iqb9NjCCTt6Hf+vOdNIZGdTiH1QSqr27H/Ek9sv/a97gfueI/5h1s3yRi1nngzMUaOOToin5dI1dXKdXiF+u0Q==",
+      "license": "MIT",
+      "dependencies": {
+        "react-remove-scroll-bar": "^2.3.7",
+        "react-style-singleton": "^2.2.3",
+        "tslib": "^2.1.0",
+        "use-callback-ref": "^1.3.3",
+        "use-sidecar": "^1.1.3"
+      },
+      "engines": {
+        "node": ">=10"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "react": "^16.8.0 || ^17.0.0 || ^18.0.0 || ^19.0.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/react-remove-scroll-bar": {
+      "version": "2.3.8",
+      "resolved": "https://registry.npmjs.org/react-remove-scroll-bar/-/react-remove-scroll-bar-2.3.8.tgz",
+      "integrity": "sha512-9r+yi9+mgU33AKcj6IbT9oRCO78WriSj6t/cF8DWBZJ9aOGPOTEDvdUDz1FwKim7QXWwmHqtdHnRJfhAxEG46Q==",
+      "license": "MIT",
+      "dependencies": {
+        "react-style-singleton": "^2.2.2",
+        "tslib": "^2.0.0"
+      },
+      "engines": {
+        "node": ">=10"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "react": "^16.8.0 || ^17.0.0 || ^18.0.0 || ^19.0.0"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/react-style-singleton": {
+      "version": "2.2.3",
+      "resolved": "https://registry.npmjs.org/react-style-singleton/-/react-style-singleton-2.2.3.tgz",
+      "integrity": "sha512-b6jSvxvVnyptAiLjbkWLE/lOnR4lfTtDAl+eUC7RZy+QQWc6wRzIV2CE6xBuMmDxc2qIihtDCZD5NPOFl7fRBQ==",
+      "license": "MIT",
+      "dependencies": {
+        "get-nonce": "^1.0.0",
+        "tslib": "^2.0.0"
+      },
+      "engines": {
+        "node": ">=10"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "react": "^16.8.0 || ^17.0.0 || ^18.0.0 || ^19.0.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        }
+      }
+    },
     "node_modules/rollup": {
       "version": "4.63.3",
       "resolved": "https://registry.npmjs.org/rollup/-/rollup-4.63.3.tgz",
       "integrity": "sha512-1i2XreiAoMMXuPGD6Msj2xWrMMkHojNRKivInxGQcg7/1KuPuYlfUutLyh4drnOxUTHX9cHI4wFoat8D/NKaBw==",
       "dev": true,
       "license": "MIT",
       "dependencies": {
         "@types/estree": "1.0.9"
       },
       "bin": {
@@ -2459,20 +4871,49 @@
       },
       "peerDependenciesMeta": {
         "@babel/core": {
           "optional": true
         },
         "babel-plugin-macros": {
           "optional": true
         }
       }
     },
+    "node_modules/tailwind-merge": {
+      "version": "3.7.0",
+      "resolved": "https://registry.npmjs.org/tailwind-merge/-/tailwind-merge-3.7.0.tgz",
+      "integrity": "sha512-XPPUyAc+cvspz3lHTcR/QgPfW2A0lv/xQNIjX3HGhLR+Nq2lHaLq5MtTesHn8GUr3W3DguT2KT5x3NVgRtYwmA==",
+      "license": "MIT",
+      "funding": {
+        "type": "github",
+        "url": "https://github.com/sponsors/dcastil"
+      }
+    },
+    "node_modules/tailwindcss": {
+      "version": "4.3.3",
+      "resolved": "https://registry.npmjs.org/tailwindcss/-/tailwindcss-4.3.3.tgz",
+      "integrity": "sha512-gOhV3P7ufE62QDGg1zVaTgCR+EtPv92k2nIhVcVKcLmxT1sUBsQGhnZj175j+MqRt4zLF7ic+sCYjfhxMxj7YQ==",
+      "license": "MIT"
+    },
+    "node_modules/tapable": {
+      "version": "2.3.3",
+      "resolved": "https://registry.npmjs.org/tapable/-/tapable-2.3.3.tgz",
+      "integrity": "sha512-uxc/zpqFg6x7C8vOE7lh6Lbda8eEL9zmVm/PLeTPBRhh1xCgdWaQ+J1CUieGpIfm2HdtsUpRv+HshiasBMcc6A==",
+      "license": "MIT",
+      "engines": {
+        "node": ">=6"
+      },
+      "funding": {
+        "type": "opencollective",
+        "url": "https://opencollective.com/webpack"
+      }
+    },
     "node_modules/tinybench": {
       "version": "2.9.0",
       "resolved": "https://registry.npmjs.org/tinybench/-/tinybench-2.9.0.tgz",
       "integrity": "sha512-0+DUvqWMValLmha6lr4kD8iAMK1HzV0/aKnCtWb9v9641TnP/MFb7Pc2bxoxQjTXAErryXVgUOfv2YqNllqGeg==",
       "dev": true,
       "license": "MIT"
     },
     "node_modules/tinyexec": {
       "version": "0.3.2",
       "resolved": "https://registry.npmjs.org/tinyexec/-/tinyexec-0.3.2.tgz",
@@ -2547,20 +4988,63 @@
         "node": ">=14.17"
       }
     },
     "node_modules/undici-types": {
       "version": "6.21.0",
       "resolved": "https://registry.npmjs.org/undici-types/-/undici-types-6.21.0.tgz",
       "integrity": "sha512-iwDZqg0QAGrg9Rav5H4n0M64c3mkR59cJ6wQp+7C4nI0gsmExaedaYLNO44eT4AtBBwjbTiGPMlt2Md0T9H9JQ==",
       "dev": true,
       "license": "MIT"
     },
+    "node_modules/use-callback-ref": {
+      "version": "1.3.3",
+      "resolved": "https://registry.npmjs.org/use-callback-ref/-/use-callback-ref-1.3.3.tgz",
+      "integrity": "sha512-jQL3lRnocaFtu3V00JToYz/4QkNWswxijDaCVNZRiRTO3HQDLsdu1ZtmIUvV4yPp+rvWm5j0y0TG/S61cuijTg==",
+      "license": "MIT",
+      "dependencies": {
+        "tslib": "^2.0.0"
+      },
+      "engines": {
+        "node": ">=10"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "react": "^16.8.0 || ^17.0.0 || ^18.0.0 || ^19.0.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        }
+      }
+    },
+    "node_modules/use-sidecar": {
+      "version": "1.1.3",
+      "resolved": "https://registry.npmjs.org/use-sidecar/-/use-sidecar-1.1.3.tgz",
+      "integrity": "sha512-Fedw0aZvkhynoPYlA5WXrMCAMm+nSWdZt6lzJQ7Ok8S6Q+VsHmHpRWndVRJ8Be0ZbkfPc5LRYH+5XrzXcEeLRQ==",
+      "license": "MIT",
+      "dependencies": {
+        "detect-node-es": "^1.1.0",
+        "tslib": "^2.0.0"
+      },
+      "engines": {
+        "node": ">=10"
+      },
+      "peerDependencies": {
+        "@types/react": "*",
+        "react": "^16.8.0 || ^17.0.0 || ^18.0.0 || ^19.0.0 || ^19.0.0-rc"
+      },
+      "peerDependenciesMeta": {
+        "@types/react": {
+          "optional": true
+        }
+      }
+    },
     "node_modules/vite": {
       "version": "7.3.6",
       "resolved": "https://registry.npmjs.org/vite/-/vite-7.3.6.tgz",
       "integrity": "sha512-4XP60spRGjSZFf1qYH+dJIkK2znL3zQfl9KkOV9MkkRR/3Dls0dxaBsQPTloEc5BLXWPL9vsOxopxyKoMmDueg==",
       "dev": true,
       "license": "MIT",
       "dependencies": {
         "esbuild": "^0.27.0 || ^0.28.0",
         "fdir": "^6.5.0",
         "picomatch": "^4.0.3",
diff --git a/Journeys/Journeys.UX/package.json b/Journeys/Journeys.UX/package.json
index 77d8b4a..16a5d88 100644
--- a/Journeys/Journeys.UX/package.json
+++ b/Journeys/Journeys.UX/package.json
@@ -3,24 +3,33 @@
   "version": "0.0.0",
   "private": true,
   "type": "module",
   "scripts": {
     "dev": "node ./scripts/dev.mjs",
     "build": "next build",
     "start": "next start",
     "test": "vitest run"
   },
   "dependencies": {
+    "@tailwindcss/postcss": "^4.3.3",
+    "@tanstack/react-query": "^5.103.1",
+    "class-variance-authority": "^0.7.1",
+    "clsx": "^2.1.1",
+    "lucide-react": "^1.47.0",
     "next": "^16.0.7",
     "next-auth": "5.0.0-beta.30",
+    "radix-ui": "^1.6.7",
     "react": "^19.0.0",
     "react-dom": "^19.0.0",
+    "react-hot-toast": "^2.6.1",
+    "tailwind-merge": "^3.7.0",
+    "tailwindcss": "^4.3.3",
     "zod": "^4.0.0"
   },
   "devDependencies": {
     "@types/node": "^22.0.0",
     "@types/react": "^19.0.0",
     "@types/react-dom": "^19.0.0",
     "typescript": "^5.6.0",
     "vitest": "^3.0.0"
   }
 }
diff --git a/Journeys/Journeys.UX/src/app/globals.css b/Journeys/Journeys.UX/src/app/globals.css
index 764c7fb..8105ece 100644
--- a/Journeys/Journeys.UX/src/app/globals.css
+++ b/Journeys/Journeys.UX/src/app/globals.css
@@ -1,16 +1,72 @@
-:root { font-family: system-ui, sans-serif; color: #111; }
-body { margin: 0; }
+@import "tailwindcss";
+
+@theme {
+  --font-sans: system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
+  --color-background: oklch(1 0 0);
+  --color-foreground: oklch(0.145 0 0);
+  --color-card: oklch(1 0 0);
+  --color-card-foreground: oklch(0.145 0 0);
+  --color-popover: oklch(1 0 0);
+  --color-popover-foreground: oklch(0.145 0 0);
+  --color-primary: oklch(0.205 0 0);
+  --color-primary-foreground: oklch(0.985 0 0);
+  --color-secondary: oklch(0.97 0 0);
+  --color-secondary-foreground: oklch(0.205 0 0);
+  --color-muted: oklch(0.97 0 0);
+  --color-muted-foreground: oklch(0.556 0 0);
+  --color-accent: oklch(0.97 0 0);
+  --color-accent-foreground: oklch(0.205 0 0);
+  --color-destructive: oklch(0.577 0.245 27.325);
+  --color-destructive-foreground: oklch(0.985 0 0);
+  --color-border: oklch(0.922 0 0);
+  --color-input: oklch(0.922 0 0);
+  --color-ring: oklch(0.708 0 0);
+}
+
+:root { font-family: var(--font-sans); color: var(--color-foreground); }
+body { margin: 0; background: var(--color-background); }
 a { color: inherit; }
 button { cursor: pointer; }
-.layout { display: flex; min-height: 100vh; }
-nav.loyalty-nav { width: 240px; padding: 1rem; background: #f4f4f5; }
-nav.loyalty-nav h1 { font-size: 1rem; margin: 0 0 1rem; }
-nav.loyalty-nav a, nav.loyalty-nav span { display: block; padding: 0.35rem 0; }
-nav.loyalty-nav .disabled { color: #888; }
-main { padding: 1.5rem; flex: 1; }
 .card-row { display: flex; gap: 1rem; }
 .card { border: 1px solid #ddd; padding: 1rem; min-width: 12rem; }
 .error { color: #b91c1c; }
 .empty { color: #555; }
 table { border-collapse: collapse; width: 100%; }
 th, td { border: 1px solid #ddd; padding: 0.4rem 0.6rem; text-align: left; }
+.agent-chat { color: #111; }
+.agent-chat .conversation-id {
+  display: flex;
+  align-items: center;
+  flex-wrap: wrap;
+  gap: 0.5rem 0.75rem;
+  margin: 0 0 1rem;
+  padding: 0.5rem 0.75rem;
+  background: #f4f4f5;
+  border: 1px solid #d4d4d8;
+  border-radius: 4px;
+  font-size: 0.875rem;
+  color: #111;
+}
+.agent-chat .conversation-id-label { font-weight: 600; color: #3f3f46; }
+.agent-chat .conversation-id code {
+  font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
+  user-select: all;
+  word-break: break-all;
+  color: #111;
+}
+.agent-chat .conversation-id button {
+  display: inline-flex;
+  align-items: center;
+  justify-content: center;
+  flex-shrink: 0;
+  padding: 0.2rem;
+  border: 1px solid #d4d4d8;
+  border-radius: 4px;
+  background: #fff;
+  color: #111;
+  line-height: 0;
+}
+.agent-chat .transcript { display: flex; flex-direction: column; gap: 0.75rem; }
+.agent-chat form { margin-top: 1rem; display: flex; flex-direction: column; align-items: stretch; gap: 0.5rem; }
+.agent-chat textarea { width: 100%; min-height: 4.5rem; resize: vertical; box-sizing: border-box; }
+.agent-chat form button { align-self: flex-start; }
diff --git a/Journeys/Journeys.UX/src/app/layout.tsx b/Journeys/Journeys.UX/src/app/layout.tsx
index 0559622..23494db 100644
--- a/Journeys/Journeys.UX/src/app/layout.tsx
+++ b/Journeys/Journeys.UX/src/app/layout.tsx
@@ -1,12 +1,15 @@
 import type { ReactNode } from "react";
+import { Providers } from "@/components/providers";
 import "./globals.css";
 
 export const metadata = { title: "Journeys" };
 
 export default function RootLayout({ children }: { children: ReactNode }) {
   return (
     <html lang="en">
-      <body>{children}</body>
+      <body>
+        <Providers>{children}</Providers>
+      </body>
     </html>
   );
 }
diff --git a/Journeys/Journeys.UX/src/app/loyalty/layout.tsx b/Journeys/Journeys.UX/src/app/loyalty/layout.tsx
index e89e8c2..c2854cc 100644
--- a/Journeys/Journeys.UX/src/app/loyalty/layout.tsx
+++ b/Journeys/Journeys.UX/src/app/loyalty/layout.tsx
@@ -1,16 +1,16 @@
 import type { ReactNode } from "react";
 import { redirect } from "next/navigation";
 import { auth } from "@/auth";
 import { LoyaltyNav } from "@/components/loyalty-nav";
 
 export default async function LoyaltyLayout({ children }: { children: ReactNode }) {
   const session = await auth();
   if (!session?.user) redirect("/signin");
 
   return (
-    <div className="layout">
+    <div className="flex min-h-screen bg-white text-zinc-950">
       <LoyaltyNav />
-      <main>{children}</main>
+      <main className="min-w-0 flex-1 p-6">{children}</main>
     </div>
   );
 }
diff --git a/Journeys/Journeys.UX/src/components/loyalty-nav.tsx b/Journeys/Journeys.UX/src/components/loyalty-nav.tsx
index a9e62ed..2922fb2 100644
--- a/Journeys/Journeys.UX/src/components/loyalty-nav.tsx
+++ b/Journeys/Journeys.UX/src/components/loyalty-nav.tsx
@@ -1,20 +1,23 @@
 import Link from "next/link";
 
 export function LoyaltyNav() {
+  const linkClass = "block rounded-md px-3 py-2 text-sm text-zinc-700 hover:bg-zinc-200 hover:text-zinc-950";
+  const disabledClass = "block px-3 py-2 text-sm text-zinc-400";
+
   return (
-    <nav className="loyalty-nav">
-      <h1>Loyalty</h1>
-      <Link href="/loyalty">Overview</Link>
-      <Link href="/loyalty/accounts">Accounts</Link>
-      <Link href="/loyalty/campaigns">Campaigns</Link>
-      <span className="disabled">Promotions</span>
-      <span className="disabled">Analytics</span>
-      <span className="disabled">Action Log</span>
-      <span className="disabled">Notifications</span>
-      <span className="disabled">File Ingestion</span>
-      <span className="disabled">Settings</span>
-      <span className="disabled">Data Explorer</span>
-      <span className="disabled">Model Builder</span>
+    <nav className="w-60 shrink-0 border-r border-zinc-200 bg-zinc-100 p-4">
+      <h1 className="mb-4 px-3 text-base font-semibold text-zinc-950">Loyalty</h1>
+      <Link className={linkClass} href="/loyalty">Overview</Link>
+      <Link className={linkClass} href="/loyalty/accounts">Accounts</Link>
+      <Link className={linkClass} href="/loyalty/campaigns">Campaigns</Link>
+      <span className={disabledClass}>Promotions</span>
+      <span className={disabledClass}>Analytics</span>
+      <span className={disabledClass}>Action Log</span>
+      <span className={disabledClass}>Notifications</span>
+      <span className={disabledClass}>File Ingestion</span>
+      <span className={disabledClass}>Settings</span>
+      <span className={disabledClass}>Data Explorer</span>
+      <span className={disabledClass}>Model Builder</span>
     </nav>
   );
 }
diff --git a/Journeys/Journeys.UX/src/lib/api-types.ts b/Journeys/Journeys.UX/src/lib/api-types.ts
index 25bd769..8a6f99d 100644
--- a/Journeys/Journeys.UX/src/lib/api-types.ts
+++ b/Journeys/Journeys.UX/src/lib/api-types.ts
@@ -3,19 +3,30 @@ export type ApiResponse<T> = {
   data?: T;
   error?: string;
   timestamp: string;
 };
 
 export type CampaignListItem = {
   id?: string;
   name?: string;
   status?: string;
   extCampaignId?: string;
+  startDate?: string;
+  endDate?: string;
+  events?: string[];
+  journey?: Record<string, unknown>;
+  [key: string]: unknown;
 };
 
+export type AgentConversationListItem = {
+  conversationId: string;
+};
+
+export type PointAccountTypeListItem = Record<string, unknown>;
+
 export type SchemaListItem = {
   id?: string;
   name?: string;
   status?: string;
   modelType?: string;
   attributes?: { name?: string; displayName?: string }[];
 };
diff --git a/Journeys/Journeys.UX/src/lib/journeys-fetch.test.ts b/Journeys/Journeys.UX/src/lib/journeys-fetch.test.ts
index 7670e61..f526e8a 100644
--- a/Journeys/Journeys.UX/src/lib/journeys-fetch.test.ts
+++ b/Journeys/Journeys.UX/src/lib/journeys-fetch.test.ts
@@ -15,21 +15,21 @@ describe("journeysFetch", () => {
       apiBaseUrl: "https://api.example"
     });
     expect(fetchMock).not.toHaveBeenCalled();
     expect(r.success).toBe(false);
     expect(r.error).toMatch(/Not authenticated/i);
   });
 
   it("does not call fetch when path is not allowlisted", async () => {
     vi.stubEnv("JOURNEYS_TENANT_ID", "");
     const fetchMock = vi.fn();
-    const r = await journeysFetch("campaigns/x/save", {}, {
+    const r = await journeysFetch("campaigns/session/pointaccounttype/upsert", {}, {
       getSession: async () => session(),
       fetch: fetchMock as unknown as typeof fetch,
       apiBaseUrl: "https://api.example"
     });
     expect(fetchMock).not.toHaveBeenCalled();
     expect(r.error).toMatch(/not-allowlisted/);
   });
 
   it("POSTs getall with API key header and wraps entities", async () => {
     vi.stubEnv("JOURNEYS_TENANT_ID", "");
@@ -66,11 +66,36 @@ describe("journeysFetch", () => {
     vi.stubEnv("JOURNEYS_TENANT_ID", "TestTenant1");
     const fetchMock = vi.fn(async () => new Response("[]", { status: 200 }));
     await journeysFetch("campaigns/x/getall", { method: "POST", body: {} }, {
       getSession: async () => session({ tenantId: "stale-session-tenant" }),
       fetch: fetchMock as unknown as typeof fetch,
       apiBaseUrl: "https://api.example"
     });
     const [url] = fetchMock.mock.calls[0] as unknown as [string];
     expect(url).toBe("https://api.example/api/Campaign/TestTenant1/getall");
   });
+
+  it("appends encoded non-empty search params without sending Cookie", async () => {
+    vi.stubEnv("JOURNEYS_TENANT_ID", "");
+    const fetchMock = vi.fn(async () => new Response("{}", { status: 200 }));
+    await journeysFetch(
+      "campaigns/session/cid-1",
+      {
+        searchParams: {
+          campaignStatus: "draft version",
+          empty: "",
+          omitted: undefined
+        }
+      },
+      {
+        getSession: async () => session(),
+        fetch: fetchMock as unknown as typeof fetch,
+        apiBaseUrl: "https://api.example"
+      }
+    );
+    const [url, init] = fetchMock.mock.calls[0] as unknown as [string, RequestInit];
+    expect(url).toBe(
+      "https://api.example/api/Campaign/acme/cid-1?campaignStatus=draft+version"
+    );
+    expect((init.headers as Record<string, string>).Cookie).toBeUndefined();
+  });
 });
diff --git a/Journeys/Journeys.UX/src/lib/journeys-fetch.ts b/Journeys/Journeys.UX/src/lib/journeys-fetch.ts
index 27d3f10..6ac2e2d 100644
--- a/Journeys/Journeys.UX/src/lib/journeys-fetch.ts
+++ b/Journeys/Journeys.UX/src/lib/journeys-fetch.ts
@@ -7,20 +7,21 @@ import { wrapApiEnvelope } from "./wrap-api-envelope";
 export type JourneysSession = {
   userId: string;
   tenantId: string;
   accessToken?: string;
   apiKey?: string;
 };
 
 export type JourneysFetchOptions = {
   method?: string;
   body?: unknown;
+  searchParams?: Record<string, string | undefined>;
 };
 
 export type JourneysFetchDeps = {
   getSession: () => Promise<JourneysSession | null>;
   fetch: typeof fetch;
   apiBaseUrl: string;
 };
 
 export async function journeysFetch<T>(
   path: string,
@@ -46,20 +47,26 @@ export async function journeysFetch<T>(
     return { success: false, error: "JOURNEYS_API_BASE_URL is not configured", timestamp };
   }
 
   let apiPath: string;
   try {
     apiPath = mapLoyaltyPath(path, tenantId);
   } catch (e) {
     const message = e instanceof Error ? e.message : String(e);
     return { success: false, error: message, timestamp };
   }
+  const searchParams = new URLSearchParams();
+  for (const [key, value] of Object.entries(options.searchParams ?? {})) {
+    if (value) searchParams.set(key, value);
+  }
+  const query = searchParams.toString();
+  if (query) apiPath += `?${query}`;
 
   const headers: Record<string, string> = { Accept: "application/json", "Content-Type": "application/json" };
   if (session.accessToken) {
     headers.Authorization = `Bearer ${session.accessToken}`;
   } else if (session.apiKey) {
     headers["Journeys-API-KEY"] = session.apiKey;
   } else {
     return { success: false, error: "No credentials in session", timestamp };
   }
 
diff --git a/Journeys/Journeys.UX/src/lib/map-loyalty-path.test.ts b/Journeys/Journeys.UX/src/lib/map-loyalty-path.test.ts
index 71b4d6f..f52c22e 100644
--- a/Journeys/Journeys.UX/src/lib/map-loyalty-path.test.ts
+++ b/Journeys/Journeys.UX/src/lib/map-loyalty-path.test.ts
@@ -14,17 +14,62 @@ describe("mapLoyaltyPath", () => {
     );
   });
 
   it("maps events admin query and preserves schema name", () => {
     expect(mapLoyaltyPath("events/slug/LoyaltyAccountDetails/admin/query", "acme")).toBe(
       "/api/Events/acme/LoyaltyAccountDetails/admin/query"
     );
   });
 
   it("rejects unknown paths", () => {
-    expect(() => mapLoyaltyPath("campaigns/slug/save", "acme")).toThrow(/not-allowlisted:/);
+    expect(() => mapLoyaltyPath("campaigns/slug/cid-1/stats", "acme")).toThrow(/not-allowlisted:/);
   });
 
   it("rejects empty tenantId", () => {
     expect(() => mapLoyaltyPath("campaigns/x/getall", "")).toThrow(/tenant/i);
   });
+
+  const t = "acme";
+
+  it("maps getmany, save, validate", () => {
+    expect(mapLoyaltyPath("campaigns/session/getmany", t)).toBe("/api/Campaign/acme/getmany");
+    expect(mapLoyaltyPath("campaigns/session/save", t)).toBe("/api/Campaign/acme/save");
+    expect(mapLoyaltyPath("campaigns/session/validate", t)).toBe("/api/Campaign/acme/validate");
+  });
+
+  it("maps get-by-id, delete, copy, restore", () => {
+    expect(mapLoyaltyPath("campaigns/session/cid-1", t)).toBe("/api/Campaign/acme/cid-1");
+    expect(mapLoyaltyPath("campaigns/session/cid-1/copy", t)).toBe("/api/Campaign/acme/cid-1/copy");
+    expect(mapLoyaltyPath("campaigns/session/cid-1/restore", t)).toBe(
+      "/api/Campaign/acme/cid-1/restore"
+    );
+  });
+
+  it("maps versions, archived, live/draft by ext, PAT getall", () => {
+    expect(mapLoyaltyPath("campaigns/session/versions/ext-1", t)).toBe(
+      "/api/Campaign/acme/versions/ext-1"
+    );
+    expect(mapLoyaltyPath("campaigns/session/archived", t)).toBe("/api/Campaign/acme/archived");
+    expect(mapLoyaltyPath("campaigns/session/live/ext-1", t)).toBe(
+      "/api/Campaign/acme/live/ext-1"
+    );
+    expect(mapLoyaltyPath("campaigns/session/draft/ext-1", t)).toBe(
+      "/api/Campaign/acme/draft/ext-1"
+    );
+    expect(mapLoyaltyPath("campaigns/session/pointaccounttype/getall", t)).toBe(
+      "/api/Campaign/acme/pointaccounttype/getall"
+    );
+  });
+
+  it("maps campaign-agent conversations JSON", () => {
+    expect(mapLoyaltyPath("campaign-agent/conversations", t)).toBe(
+      "/api/v1/acme/campaign-agent/conversations"
+    );
+  });
+
+  it("still rejects unknown paths", () => {
+    expect(() => mapLoyaltyPath("campaigns/session/pointaccounttype/upsert", t)).toThrow(
+      /not-allowlisted:/
+    );
+    expect(() => mapLoyaltyPath("campaigns/session/cid-1/stats", t)).toThrow(/not-allowlisted:/);
+  });
 });
diff --git a/Journeys/Journeys.UX/src/lib/map-loyalty-path.ts b/Journeys/Journeys.UX/src/lib/map-loyalty-path.ts
index c2b1c45..f0c319e 100644
--- a/Journeys/Journeys.UX/src/lib/map-loyalty-path.ts
+++ b/Journeys/Journeys.UX/src/lib/map-loyalty-path.ts
@@ -1,17 +1,59 @@
 export function mapLoyaltyPath(path: string, tenantId: string): string {
   const tenant = tenantId.trim();
   if (!tenant) throw new Error("tenantId is required");
   const trimmed = path.replace(/^\/+/, "");
 
   if (/^campaigns\/[^/]+\/getall$/i.test(trimmed)) {
     return `/api/Campaign/${encodeURIComponent(tenant)}/getall`;
   }
+  if (/^campaigns\/[^/]+\/getmany$/i.test(trimmed)) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}/getmany`;
+  }
+  if (/^campaigns\/[^/]+\/save$/i.test(trimmed)) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}/save`;
+  }
+  if (/^campaigns\/[^/]+\/validate$/i.test(trimmed)) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}/validate`;
+  }
+  if (/^campaigns\/[^/]+\/archived$/i.test(trimmed)) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}/archived`;
+  }
+  if (/^campaigns\/[^/]+\/pointaccounttype\/getall$/i.test(trimmed)) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}/pointaccounttype/getall`;
+  }
+  const versions = /^campaigns\/[^/]+\/versions\/([^/]+)$/i.exec(trimmed);
+  if (versions) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}/versions/${encodeURIComponent(versions[1])}`;
+  }
+  const live = /^campaigns\/[^/]+\/live\/([^/]+)$/i.exec(trimmed);
+  if (live) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}/live/${encodeURIComponent(live[1])}`;
+  }
+  const draft = /^campaigns\/[^/]+\/draft\/([^/]+)$/i.exec(trimmed);
+  if (draft) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}/draft/${encodeURIComponent(draft[1])}`;
+  }
+  const copy = /^campaigns\/[^/]+\/([^/]+)\/copy$/i.exec(trimmed);
+  if (copy) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}/${encodeURIComponent(copy[1])}/copy`;
+  }
+  const restore = /^campaigns\/[^/]+\/([^/]+)\/restore$/i.exec(trimmed);
+  if (restore) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}/${encodeURIComponent(restore[1])}/restore`;
+  }
+  const one = /^campaigns\/[^/]+\/([^/]+)$/i.exec(trimmed);
+  if (one && !/^(getall|getmany|save|validate|archived)$/i.test(one[1])) {
+    return `/api/Campaign/${encodeURIComponent(tenant)}/${encodeURIComponent(one[1])}`;
+  }
+  if (/^campaign-agent\/conversations$/i.test(trimmed)) {
+    return `/api/v1/${encodeURIComponent(tenant)}/campaign-agent/conversations`;
+  }
   if (/^schemas\/[^/]+\/model\/all$/i.test(trimmed)) {
     return `/api/Model/${encodeURIComponent(tenant)}/GetMany`;
   }
   const events = /^events\/[^/]+\/([^/]+)\/admin\/query$/i.exec(trimmed);
   if (events) {
     return `/api/Events/${encodeURIComponent(tenant)}/${encodeURIComponent(events[1])}/admin/query`;
   }
   throw new Error(`not-allowlisted: ${trimmed}`);
 }
diff --git a/Journeys/Journeys.UX/src/services/loyalty/actions.ts b/Journeys/Journeys.UX/src/services/loyalty/actions.ts
index af313d2..785675f 100644
--- a/Journeys/Journeys.UX/src/services/loyalty/actions.ts
+++ b/Journeys/Journeys.UX/src/services/loyalty/actions.ts
@@ -1,28 +1,151 @@
 "use server";
 
-import type { ApiResponse, CampaignListItem, SchemaListItem } from "@/lib/api-types";
+import type {
+  AgentConversationListItem,
+  ApiResponse,
+  CampaignListItem,
+  PointAccountTypeListItem,
+  SchemaListItem
+} from "@/lib/api-types";
 import { journeysFetch } from "@/lib/journeys-fetch";
 import { getManyModelsListBody, isUsableModelId, LOYALTY_MODEL_TYPE } from "@/lib/loyalty-model";
 import { extractEntities, normalizeSchema, pickLiveSchema } from "./parse-list";
 
 const TENANT_SLUG = "session";
 
 export async function getCampaigns(): Promise<ApiResponse<CampaignListItem[]>> {
   const res = await journeysFetch<unknown>(`campaigns/${TENANT_SLUG}/getall`, {
     method: "POST",
     body: { pageSize: 100, continuationToken: null }
   });
   if (!res.success) return res as ApiResponse<CampaignListItem[]>;
   return { ...res, data: extractEntities(res.data) as CampaignListItem[] };
 }
 
+export async function getCampaignsByFilters(
+  filters: Record<string, unknown>
+): Promise<ApiResponse<CampaignListItem[]>> {
+  const res = await journeysFetch<unknown>(`campaigns/${TENANT_SLUG}/getmany`, {
+    method: "POST",
+    body: filters
+  });
+  if (!res.success) return res as ApiResponse<CampaignListItem[]>;
+  return { ...res, data: extractEntities(res.data) as CampaignListItem[] };
+}
+
+export async function getCampaign(
+  id: string,
+  status?: string
+): Promise<ApiResponse<CampaignListItem>> {
+  return journeysFetch<CampaignListItem>(`campaigns/${TENANT_SLUG}/${id}`, {
+    searchParams: { campaignStatus: status }
+  });
+}
+
+export async function updateCampaign(
+  data: CampaignListItem
+): Promise<ApiResponse<CampaignListItem>> {
+  return journeysFetch<CampaignListItem>(`campaigns/${TENANT_SLUG}/save`, {
+    method: "POST",
+    body: data
+  });
+}
+
+export async function deleteCampaign(
+  id: string,
+  status: string
+): Promise<ApiResponse<void>> {
+  return journeysFetch<void>(`campaigns/${TENANT_SLUG}/${id}`, {
+    method: "DELETE",
+    searchParams: { status }
+  });
+}
+
+export async function copyCampaign(
+  id: string,
+  status: string,
+  name?: string
+): Promise<ApiResponse<CampaignListItem>> {
+  return journeysFetch<CampaignListItem>(`campaigns/${TENANT_SLUG}/${id}/copy`, {
+    method: "POST",
+    searchParams: { status },
+    body: name ? { name } : undefined
+  });
+}
+
+export async function restoreCampaign(id: string): Promise<ApiResponse<CampaignListItem>> {
+  return journeysFetch<CampaignListItem>(`campaigns/${TENANT_SLUG}/${id}/restore`, {
+    method: "POST",
+    searchParams: { status: "archive" }
+  });
+}
+
+export async function validateCampaign(
+  data: CampaignListItem
+): Promise<ApiResponse<unknown>> {
+  return journeysFetch<unknown>(`campaigns/${TENANT_SLUG}/validate`, {
+    method: "POST",
+    body: data
+  });
+}
+
+export async function getCampaignVersions(
+  ext: string
+): Promise<ApiResponse<CampaignListItem[]>> {
+  const res = await journeysFetch<unknown>(`campaigns/${TENANT_SLUG}/versions/${ext}`);
+  if (!res.success) return res as ApiResponse<CampaignListItem[]>;
+  return { ...res, data: extractEntities(res.data) as CampaignListItem[] };
+}
+
+export async function getArchivedCampaigns(): Promise<ApiResponse<CampaignListItem[]>> {
+  const res = await journeysFetch<unknown>(`campaigns/${TENANT_SLUG}/archived`);
+  if (!res.success) return res as ApiResponse<CampaignListItem[]>;
+  return { ...res, data: extractEntities(res.data) as CampaignListItem[] };
+}
+
+export async function getLiveByExt(ext: string): Promise<ApiResponse<CampaignListItem>> {
+  return journeysFetch<CampaignListItem>(`campaigns/${TENANT_SLUG}/live/${ext}`);
+}
+
+export async function getDraftByExt(ext: string): Promise<ApiResponse<CampaignListItem>> {
+  return journeysFetch<CampaignListItem>(`campaigns/${TENANT_SLUG}/draft/${ext}`);
+}
+
+export async function getPointAccountTypes(): Promise<ApiResponse<PointAccountTypeListItem[]>> {
+  const res = await journeysFetch<unknown>(
+    `campaigns/${TENANT_SLUG}/pointaccounttype/getall`,
+    {
+      method: "POST",
+      body: { pageSize: 100, continuationToken: null }
+    }
+  );
+  if (!res.success) return res as ApiResponse<PointAccountTypeListItem[]>;
+  return { ...res, data: extractEntities(res.data) as PointAccountTypeListItem[] };
+}
+
+export async function listAgentConversations(): Promise<
+  ApiResponse<AgentConversationListItem[]>
+> {
+  const res = await journeysFetch<unknown>("campaign-agent/conversations");
+  if (!res.success) return res as ApiResponse<AgentConversationListItem[]>;
+  const items = extractEntities(res.data).filter(
+    (item): item is AgentConversationListItem =>
+      Boolean(
+        item &&
+          typeof item === "object" &&
+          typeof (item as { conversationId?: unknown }).conversationId === "string"
+      )
+  );
+  return { ...res, data: items };
+}
+
 export async function getAllSchemas(): Promise<ApiResponse<SchemaListItem[]>> {
   const res = await journeysFetch<unknown>(`schemas/${TENANT_SLUG}/model/all`, {
     method: "POST",
     body: getManyModelsListBody()
   });
   if (!res.success) return res as ApiResponse<SchemaListItem[]>;
   const schemas = extractEntities(res.data)
     .map(normalizeSchema)
     .filter((s): s is SchemaListItem => s !== null)
     .filter((s) => !s.modelType || s.modelType.toLowerCase() === LOYALTY_MODEL_TYPE);

### NEW FILE Journeys/Journeys.UX/postcss.config.mjs

diff --git a/Journeys/Journeys.UX/postcss.config.mjs b/Journeys/Journeys.UX/postcss.config.mjs
new file mode 100644
index 0000000..8f57ef5
--- /dev/null
+++ b/Journeys/Journeys.UX/postcss.config.mjs
@@ -0,0 +1,2 @@
+const postcssConfig = { plugins: { "@tailwindcss/postcss": {} } };
+export default postcssConfig;

### NEW FILE Journeys/Journeys.UX/src/components/providers.tsx

diff --git a/Journeys/Journeys.UX/src/components/providers.tsx b/Journeys/Journeys.UX/src/components/providers.tsx
new file mode 100644
index 0000000..ab26944
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/providers.tsx
@@ -0,0 +1,16 @@
+"use client";
+
+import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
+import { useState, type ReactNode } from "react";
+import { Toaster } from "react-hot-toast";
+
+export function Providers({ children }: { children: ReactNode }) {
+  const [queryClient] = useState(() => new QueryClient());
+
+  return (
+    <QueryClientProvider client={queryClient}>
+      {children}
+      <Toaster />
+    </QueryClientProvider>
+  );
+}

### NEW FILE Journeys/Journeys.UX/src/components/ui/alert-dialog.tsx

diff --git a/Journeys/Journeys.UX/src/components/ui/alert-dialog.tsx b/Journeys/Journeys.UX/src/components/ui/alert-dialog.tsx
new file mode 100644
index 0000000..6efed37
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/ui/alert-dialog.tsx
@@ -0,0 +1,80 @@
+"use client";
+
+import * as React from "react";
+import { AlertDialog as AlertDialogPrimitive } from "radix-ui";
+
+import { Button } from "@/components/ui/button";
+import { cn } from "@/lib/utils";
+
+function AlertDialog(props: React.ComponentProps<typeof AlertDialogPrimitive.Root>) {
+  return <AlertDialogPrimitive.Root data-slot="alert-dialog" {...props} />;
+}
+function AlertDialogTrigger(props: React.ComponentProps<typeof AlertDialogPrimitive.Trigger>) {
+  return <AlertDialogPrimitive.Trigger data-slot="alert-dialog-trigger" {...props} />;
+}
+function AlertDialogPortal(props: React.ComponentProps<typeof AlertDialogPrimitive.Portal>) {
+  return <AlertDialogPrimitive.Portal data-slot="alert-dialog-portal" {...props} />;
+}
+function AlertDialogOverlay({ className, ...props }: React.ComponentProps<typeof AlertDialogPrimitive.Overlay>) {
+  return (
+    <AlertDialogPrimitive.Overlay
+      data-slot="alert-dialog-overlay"
+      className={cn("fixed inset-0 z-50 bg-black/50 data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=open]:animate-in data-[state=open]:fade-in-0", className)}
+      {...props}
+    />
+  );
+}
+function AlertDialogContent({
+  className,
+  size = "default",
+  ...props
+}: React.ComponentProps<typeof AlertDialogPrimitive.Content> & { size?: "default" | "sm" }) {
+  return (
+    <AlertDialogPortal>
+      <AlertDialogOverlay />
+      <AlertDialogPrimitive.Content
+        data-slot="alert-dialog-content"
+        data-size={size}
+        className={cn("group/alert-dialog-content fixed top-[50%] left-[50%] z-50 grid w-full max-w-[calc(100%-2rem)] translate-x-[-50%] translate-y-[-50%] gap-4 rounded-lg border bg-background p-6 shadow-lg duration-200 data-[size=sm]:max-w-xs data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:zoom-out-95 data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-95 data-[size=default]:sm:max-w-lg", className)}
+        {...props}
+      />
+    </AlertDialogPortal>
+  );
+}
+function AlertDialogHeader({ className, ...props }: React.ComponentProps<"div">) {
+  return <div data-slot="alert-dialog-header" className={cn("grid grid-rows-[auto_1fr] place-items-center gap-1.5 text-center has-data-[slot=alert-dialog-media]:grid-rows-[auto_auto_1fr] has-data-[slot=alert-dialog-media]:gap-x-6 sm:group-data-[size=default]/alert-dialog-content:place-items-start sm:group-data-[size=default]/alert-dialog-content:text-left sm:group-data-[size=default]/alert-dialog-content:has-data-[slot=alert-dialog-media]:grid-rows-[auto_1fr]", className)} {...props} />;
+}
+function AlertDialogFooter({ className, ...props }: React.ComponentProps<"div">) {
+  return <div data-slot="alert-dialog-footer" className={cn("flex flex-col-reverse gap-2 group-data-[size=sm]/alert-dialog-content:grid group-data-[size=sm]/alert-dialog-content:grid-cols-2 sm:flex-row sm:justify-end", className)} {...props} />;
+}
+function AlertDialogTitle({ className, ...props }: React.ComponentProps<typeof AlertDialogPrimitive.Title>) {
+  return <AlertDialogPrimitive.Title data-slot="alert-dialog-title" className={cn("text-lg font-semibold sm:group-data-[size=default]/alert-dialog-content:group-has-data-[slot=alert-dialog-media]/alert-dialog-content:col-start-2", className)} {...props} />;
+}
+function AlertDialogDescription({ className, ...props }: React.ComponentProps<typeof AlertDialogPrimitive.Description>) {
+  return <AlertDialogPrimitive.Description data-slot="alert-dialog-description" className={cn("text-sm text-muted-foreground", className)} {...props} />;
+}
+function AlertDialogMedia({ className, ...props }: React.ComponentProps<"div">) {
+  return <div data-slot="alert-dialog-media" className={cn("mb-2 inline-flex size-16 items-center justify-center rounded-md bg-muted sm:group-data-[size=default]/alert-dialog-content:row-span-2 *:[svg:not([class*='size-'])]:size-8", className)} {...props} />;
+}
+function AlertDialogAction({
+  className,
+  variant = "default",
+  size = "default",
+  ...props
+}: React.ComponentProps<typeof AlertDialogPrimitive.Action> & Pick<React.ComponentProps<typeof Button>, "variant" | "size">) {
+  return <Button variant={variant} size={size} asChild><AlertDialogPrimitive.Action data-slot="alert-dialog-action" className={cn(className)} {...props} /></Button>;
+}
+function AlertDialogCancel({
+  className,
+  variant = "outline",
+  size = "default",
+  ...props
+}: React.ComponentProps<typeof AlertDialogPrimitive.Cancel> & Pick<React.ComponentProps<typeof Button>, "variant" | "size">) {
+  return <Button variant={variant} size={size} asChild><AlertDialogPrimitive.Cancel data-slot="alert-dialog-cancel" className={cn(className)} {...props} /></Button>;
+}
+
+export {
+  AlertDialog, AlertDialogAction, AlertDialogCancel, AlertDialogContent,
+  AlertDialogDescription, AlertDialogFooter, AlertDialogHeader, AlertDialogMedia,
+  AlertDialogOverlay, AlertDialogPortal, AlertDialogTitle, AlertDialogTrigger,
+};

### NEW FILE Journeys/Journeys.UX/src/components/ui/alert.tsx

diff --git a/Journeys/Journeys.UX/src/components/ui/alert.tsx b/Journeys/Journeys.UX/src/components/ui/alert.tsx
new file mode 100644
index 0000000..f60af9e
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/ui/alert.tsx
@@ -0,0 +1,38 @@
+import * as React from "react";
+import { cva, type VariantProps } from "class-variance-authority";
+
+import { cn } from "@/lib/utils";
+
+const alertVariants = cva(
+  "relative grid w-full grid-cols-[0_1fr] items-start gap-y-0.5 rounded-lg border px-4 py-3 text-sm has-[>svg]:grid-cols-[calc(var(--spacing)*4)_1fr] has-[>svg]:gap-x-3 [&>svg]:size-4 [&>svg]:translate-y-0.5 [&>svg]:text-current",
+  {
+    variants: {
+      variant: {
+        default: "bg-card text-card-foreground",
+        destructive:
+          "bg-card text-destructive *:data-[slot=alert-description]:text-destructive/90 [&>svg]:text-current",
+      },
+    },
+    defaultVariants: { variant: "default" },
+  }
+);
+
+function Alert({ className, variant, ...props }: React.ComponentProps<"div"> & VariantProps<typeof alertVariants>) {
+  return <div data-slot="alert" role="alert" className={cn(alertVariants({ variant }), className)} {...props} />;
+}
+
+function AlertTitle({ className, ...props }: React.ComponentProps<"div">) {
+  return <div data-slot="alert-title" className={cn("col-start-2 line-clamp-1 min-h-4 font-medium tracking-tight", className)} {...props} />;
+}
+
+function AlertDescription({ className, ...props }: React.ComponentProps<"div">) {
+  return (
+    <div
+      data-slot="alert-description"
+      className={cn("col-start-2 grid justify-items-start gap-1 text-sm text-muted-foreground [&_p]:leading-relaxed", className)}
+      {...props}
+    />
+  );
+}
+
+export { Alert, AlertTitle, AlertDescription };

### NEW FILE Journeys/Journeys.UX/src/components/ui/badge.tsx

diff --git a/Journeys/Journeys.UX/src/components/ui/badge.tsx b/Journeys/Journeys.UX/src/components/ui/badge.tsx
new file mode 100644
index 0000000..dd00358
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/ui/badge.tsx
@@ -0,0 +1,35 @@
+import * as React from "react";
+import { cva, type VariantProps } from "class-variance-authority";
+import { Slot } from "radix-ui";
+
+import { cn } from "@/lib/utils";
+
+const badgeVariants = cva(
+  "inline-flex w-fit shrink-0 items-center justify-center gap-1 overflow-hidden rounded-full border border-transparent px-2 py-0.5 text-xs font-medium whitespace-nowrap transition-[color,box-shadow] focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 aria-invalid:border-destructive aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40 [&>svg]:pointer-events-none [&>svg]:size-3",
+  {
+    variants: {
+      variant: {
+        default: "bg-primary text-primary-foreground [a&]:hover:bg-primary/90",
+        secondary: "bg-secondary text-secondary-foreground [a&]:hover:bg-secondary/90",
+        destructive:
+          "bg-destructive text-white focus-visible:ring-destructive/20 dark:bg-destructive/60 dark:focus-visible:ring-destructive/40 [a&]:hover:bg-destructive/90",
+        outline: "border-border text-foreground [a&]:hover:bg-accent [a&]:hover:text-accent-foreground",
+        ghost: "[a&]:hover:bg-accent [a&]:hover:text-accent-foreground",
+        link: "text-primary underline-offset-4 [a&]:hover:underline",
+      },
+    },
+    defaultVariants: { variant: "default" },
+  }
+);
+
+function Badge({
+  className,
+  variant = "default",
+  asChild = false,
+  ...props
+}: React.ComponentProps<"span"> & VariantProps<typeof badgeVariants> & { asChild?: boolean }) {
+  const Comp = asChild ? Slot.Root : "span";
+  return <Comp data-slot="badge" data-variant={variant} className={cn(badgeVariants({ variant }), className)} {...props} />;
+}
+
+export { Badge, badgeVariants };

### NEW FILE Journeys/Journeys.UX/src/components/ui/button.tsx

diff --git a/Journeys/Journeys.UX/src/components/ui/button.tsx b/Journeys/Journeys.UX/src/components/ui/button.tsx
new file mode 100644
index 0000000..a4a2c35
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/ui/button.tsx
@@ -0,0 +1,58 @@
+import * as React from "react";
+import { cva, type VariantProps } from "class-variance-authority";
+import { Slot } from "radix-ui";
+
+import { cn } from "@/lib/utils";
+
+const buttonVariants = cva(
+  "inline-flex shrink-0 items-center justify-center gap-2 rounded-md text-sm font-medium whitespace-nowrap transition-[color,background-color,border-color,box-shadow] outline-none focus-visible:border-ring focus-visible:ring-[3px] focus-visible:ring-ring/50 disabled:pointer-events-none disabled:opacity-50 aria-invalid:border-destructive aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40 [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4",
+  {
+    variants: {
+      variant: {
+        default: "bg-primary text-primary-foreground hover:bg-primary/90",
+        destructive:
+          "bg-destructive text-destructive-foreground hover:bg-destructive/90 focus-visible:ring-destructive/20 dark:bg-destructive/60 dark:focus-visible:ring-destructive/40",
+        outline:
+          "border bg-card text-foreground shadow-xs hover:bg-accent hover:text-accent-foreground dark:border-input dark:bg-input/30 dark:hover:bg-accent",
+        secondary: "bg-secondary text-secondary-foreground hover:bg-secondary/80",
+        ghost: "text-foreground hover:bg-accent hover:text-accent-foreground dark:hover:bg-accent",
+        link: "text-primary underline-offset-4 hover:underline",
+      },
+      size: {
+        default: "h-9 px-4 py-2 has-[>svg]:px-3",
+        xs: "h-6 gap-1 rounded-md px-2 text-xs has-[>svg]:px-1.5 [&_svg:not([class*='size-'])]:size-3",
+        sm: "h-8 gap-1.5 rounded-md px-3 has-[>svg]:px-2.5",
+        lg: "h-10 rounded-md px-6 has-[>svg]:px-4",
+        icon: "size-9",
+        "icon-xs": "size-6 rounded-md [&_svg:not([class*='size-'])]:size-3",
+        "icon-sm": "size-8",
+        "icon-lg": "size-10",
+      },
+    },
+    defaultVariants: { variant: "default", size: "default" },
+  }
+);
+
+function Button({
+  className,
+  variant = "default",
+  size = "default",
+  asChild = false,
+  ...props
+}: React.ComponentProps<"button"> &
+  VariantProps<typeof buttonVariants> & {
+    asChild?: boolean;
+  }) {
+  const Comp = asChild ? Slot.Root : "button";
+  return (
+    <Comp
+      data-slot="button"
+      data-variant={variant}
+      data-size={size}
+      className={cn(buttonVariants({ variant, size, className }))}
+      {...props}
+    />
+  );
+}
+
+export { Button, buttonVariants };

### NEW FILE Journeys/Journeys.UX/src/components/ui/card.tsx

diff --git a/Journeys/Journeys.UX/src/components/ui/card.tsx b/Journeys/Journeys.UX/src/components/ui/card.tsx
new file mode 100644
index 0000000..91c7682
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/ui/card.tsx
@@ -0,0 +1,52 @@
+import * as React from "react";
+
+import { cn } from "@/lib/utils";
+
+function Card({ className, ...props }: React.ComponentProps<"div">) {
+  return (
+    <div
+      data-slot="card"
+      className={cn(
+        "flex flex-col gap-3 rounded-xl border bg-card py-5 text-card-foreground",
+        "shadow-[0_1px_3px_0_rgb(0_0_0/0.07),0_1px_2px_-1px_rgb(0_0_0/0.05)]",
+        className
+      )}
+      {...props}
+    />
+  );
+}
+
+function CardHeader({ className, ...props }: React.ComponentProps<"div">) {
+  return (
+    <div
+      data-slot="card-header"
+      className={cn(
+        "@container/card-header grid auto-rows-min grid-rows-[auto_auto] items-start gap-1.5 px-5 has-data-[slot=card-action]:grid-cols-[1fr_auto] [.border-b]:pb-5",
+        className
+      )}
+      {...props}
+    />
+  );
+}
+
+function CardTitle({ className, ...props }: React.ComponentProps<"div">) {
+  return <div data-slot="card-title" className={cn("text-base font-semibold leading-snug tracking-tight", className)} {...props} />;
+}
+
+function CardDescription({ className, ...props }: React.ComponentProps<"div">) {
+  return <div data-slot="card-description" className={cn("text-xs text-muted-foreground", className)} {...props} />;
+}
+
+function CardAction({ className, ...props }: React.ComponentProps<"div">) {
+  return <div data-slot="card-action" className={cn("col-start-2 row-span-2 row-start-1 self-start justify-self-end", className)} {...props} />;
+}
+
+function CardContent({ className, ...props }: React.ComponentProps<"div">) {
+  return <div data-slot="card-content" className={cn("px-5", className)} {...props} />;
+}
+
+function CardFooter({ className, ...props }: React.ComponentProps<"div">) {
+  return <div data-slot="card-footer" className={cn("flex items-center px-5 [.border-t]:pt-5", className)} {...props} />;
+}
+
+export { Card, CardHeader, CardFooter, CardTitle, CardAction, CardDescription, CardContent };

### NEW FILE Journeys/Journeys.UX/src/components/ui/dropdown-menu.tsx

diff --git a/Journeys/Journeys.UX/src/components/ui/dropdown-menu.tsx b/Journeys/Journeys.UX/src/components/ui/dropdown-menu.tsx
new file mode 100644
index 0000000..d650b02
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/ui/dropdown-menu.tsx
@@ -0,0 +1,69 @@
+"use client";
+
+import * as React from "react";
+import { CheckIcon, ChevronRightIcon, CircleIcon } from "lucide-react";
+import { DropdownMenu as DropdownMenuPrimitive } from "radix-ui";
+
+import { cn } from "@/lib/utils";
+
+function DropdownMenu(props: React.ComponentProps<typeof DropdownMenuPrimitive.Root>) {
+  return <DropdownMenuPrimitive.Root data-slot="dropdown-menu" {...props} />;
+}
+function DropdownMenuPortal(props: React.ComponentProps<typeof DropdownMenuPrimitive.Portal>) {
+  return <DropdownMenuPrimitive.Portal data-slot="dropdown-menu-portal" {...props} />;
+}
+function DropdownMenuTrigger(props: React.ComponentProps<typeof DropdownMenuPrimitive.Trigger>) {
+  return <DropdownMenuPrimitive.Trigger data-slot="dropdown-menu-trigger" {...props} />;
+}
+function DropdownMenuContent({ className, sideOffset = 4, ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.Content>) {
+  return (
+    <DropdownMenuPrimitive.Portal>
+      <DropdownMenuPrimitive.Content
+        data-slot="dropdown-menu-content"
+        sideOffset={sideOffset}
+        className={cn("z-50 max-h-(--radix-dropdown-menu-content-available-height) min-w-[8rem] origin-(--radix-dropdown-menu-content-transform-origin) overflow-x-hidden overflow-y-auto rounded-md border bg-popover p-1 text-popover-foreground shadow-md data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2 data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:zoom-out-95 data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-95", className)}
+        {...props}
+      />
+    </DropdownMenuPrimitive.Portal>
+  );
+}
+function DropdownMenuGroup(props: React.ComponentProps<typeof DropdownMenuPrimitive.Group>) {
+  return <DropdownMenuPrimitive.Group data-slot="dropdown-menu-group" {...props} />;
+}
+function DropdownMenuItem({ className, inset, variant = "default", ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.Item> & { inset?: boolean; variant?: "default" | "destructive" }) {
+  return <DropdownMenuPrimitive.Item data-slot="dropdown-menu-item" data-inset={inset} data-variant={variant} className={cn("relative flex cursor-default items-center gap-2 rounded-sm px-2 py-1.5 text-sm outline-hidden select-none focus:bg-accent focus:text-accent-foreground data-[disabled]:pointer-events-none data-[disabled]:opacity-50 data-[inset]:pl-8 data-[variant=destructive]:text-destructive data-[variant=destructive]:focus:bg-destructive/10 data-[variant=destructive]:focus:text-destructive dark:data-[variant=destructive]:focus:bg-destructive/20 [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4 [&_svg:not([class*='text-'])]:text-muted-foreground data-[variant=destructive]:*:[svg]:text-destructive!", className)} {...props} />;
+}
+function DropdownMenuCheckboxItem({ className, children, checked, ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.CheckboxItem>) {
+  return <DropdownMenuPrimitive.CheckboxItem data-slot="dropdown-menu-checkbox-item" className={cn("relative flex cursor-default items-center gap-2 rounded-sm py-1.5 pr-2 pl-8 text-sm outline-hidden select-none focus:bg-accent focus:text-accent-foreground data-[disabled]:pointer-events-none data-[disabled]:opacity-50 [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4", className)} checked={checked} {...props}><span className="pointer-events-none absolute left-2 flex size-3.5 items-center justify-center"><DropdownMenuPrimitive.ItemIndicator><CheckIcon className="size-4" /></DropdownMenuPrimitive.ItemIndicator></span>{children}</DropdownMenuPrimitive.CheckboxItem>;
+}
+function DropdownMenuRadioGroup(props: React.ComponentProps<typeof DropdownMenuPrimitive.RadioGroup>) {
+  return <DropdownMenuPrimitive.RadioGroup data-slot="dropdown-menu-radio-group" {...props} />;
+}
+function DropdownMenuRadioItem({ className, children, ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.RadioItem>) {
+  return <DropdownMenuPrimitive.RadioItem data-slot="dropdown-menu-radio-item" className={cn("relative flex cursor-default items-center gap-2 rounded-sm py-1.5 pr-2 pl-8 text-sm outline-hidden select-none focus:bg-accent focus:text-accent-foreground data-[disabled]:pointer-events-none data-[disabled]:opacity-50 [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4", className)} {...props}><span className="pointer-events-none absolute left-2 flex size-3.5 items-center justify-center"><DropdownMenuPrimitive.ItemIndicator><CircleIcon className="size-2 fill-current" /></DropdownMenuPrimitive.ItemIndicator></span>{children}</DropdownMenuPrimitive.RadioItem>;
+}
+function DropdownMenuLabel({ className, inset, ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.Label> & { inset?: boolean }) {
+  return <DropdownMenuPrimitive.Label data-slot="dropdown-menu-label" data-inset={inset} className={cn("px-2 py-1.5 text-sm font-medium data-[inset]:pl-8", className)} {...props} />;
+}
+function DropdownMenuSeparator({ className, ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.Separator>) {
+  return <DropdownMenuPrimitive.Separator data-slot="dropdown-menu-separator" className={cn("-mx-1 my-1 h-px bg-border", className)} {...props} />;
+}
+function DropdownMenuShortcut({ className, ...props }: React.ComponentProps<"span">) {
+  return <span data-slot="dropdown-menu-shortcut" className={cn("ml-auto text-xs tracking-widest text-muted-foreground", className)} {...props} />;
+}
+function DropdownMenuSub(props: React.ComponentProps<typeof DropdownMenuPrimitive.Sub>) {
+  return <DropdownMenuPrimitive.Sub data-slot="dropdown-menu-sub" {...props} />;
+}
+function DropdownMenuSubTrigger({ className, inset, children, ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.SubTrigger> & { inset?: boolean }) {
+  return <DropdownMenuPrimitive.SubTrigger data-slot="dropdown-menu-sub-trigger" data-inset={inset} className={cn("flex cursor-default items-center gap-2 rounded-sm px-2 py-1.5 text-sm outline-hidden select-none focus:bg-accent focus:text-accent-foreground data-[inset]:pl-8 data-[state=open]:bg-accent data-[state=open]:text-accent-foreground [&_svg]:pointer-events-none [&_svg]:shrink-0 [&_svg:not([class*='size-'])]:size-4 [&_svg:not([class*='text-'])]:text-muted-foreground", className)} {...props}>{children}<ChevronRightIcon className="ml-auto size-4" /></DropdownMenuPrimitive.SubTrigger>;
+}
+function DropdownMenuSubContent({ className, ...props }: React.ComponentProps<typeof DropdownMenuPrimitive.SubContent>) {
+  return <DropdownMenuPrimitive.SubContent data-slot="dropdown-menu-sub-content" className={cn("z-50 min-w-[8rem] origin-(--radix-dropdown-menu-content-transform-origin) overflow-hidden rounded-md border bg-popover p-1 text-popover-foreground shadow-lg data-[side=bottom]:slide-in-from-top-2 data-[side=left]:slide-in-from-right-2 data-[side=right]:slide-in-from-left-2 data-[side=top]:slide-in-from-bottom-2 data-[state=closed]:animate-out data-[state=closed]:fade-out-0 data-[state=closed]:zoom-out-95 data-[state=open]:animate-in data-[state=open]:fade-in-0 data-[state=open]:zoom-in-95", className)} {...props} />;
+}
+
+export {
+  DropdownMenu, DropdownMenuPortal, DropdownMenuTrigger, DropdownMenuContent,
+  DropdownMenuGroup, DropdownMenuLabel, DropdownMenuItem, DropdownMenuCheckboxItem,
+  DropdownMenuRadioGroup, DropdownMenuRadioItem, DropdownMenuSeparator,
+  DropdownMenuShortcut, DropdownMenuSub, DropdownMenuSubTrigger, DropdownMenuSubContent,
+};

### NEW FILE Journeys/Journeys.UX/src/components/ui/skeleton.tsx

diff --git a/Journeys/Journeys.UX/src/components/ui/skeleton.tsx b/Journeys/Journeys.UX/src/components/ui/skeleton.tsx
new file mode 100644
index 0000000..152fae6
--- /dev/null
+++ b/Journeys/Journeys.UX/src/components/ui/skeleton.tsx
@@ -0,0 +1,7 @@
+import { cn } from "@/lib/utils";
+
+function Skeleton({ className, ...props }: React.ComponentProps<"div">) {
+  return <div data-slot="skeleton" className={cn("animate-pulse rounded-md bg-muted", className)} {...props} />;
+}
+
+export { Skeleton };

### NEW FILE Journeys/Journeys.UX/src/lib/utils.ts

diff --git a/Journeys/Journeys.UX/src/lib/utils.ts b/Journeys/Journeys.UX/src/lib/utils.ts
new file mode 100644
index 0000000..a5ef193
--- /dev/null
+++ b/Journeys/Journeys.UX/src/lib/utils.ts
@@ -0,0 +1,6 @@
+import { clsx, type ClassValue } from "clsx";
+import { twMerge } from "tailwind-merge";
+
+export function cn(...inputs: ClassValue[]) {
+  return twMerge(clsx(inputs));
+}
