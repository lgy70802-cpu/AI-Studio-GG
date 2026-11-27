# DeskDisplay Development Rules

This is a Windows desktop display application.

## Technology

- C#
- .NET 8
- WPF
- MVVM

## Core architecture

DataProvider → Scene → Renderer → RenderTarget

Render targets:

- Preview
- External Windows Display

## Important rules

1. Never assume Display 2 is the target device.
2. Do not hard-code D92 resolution.
3. Display logic must support arbitrary resolutions.
4. Preview and external display must share the same Scene model.
5. Do not access hardware sensor libraries directly from UI.
6. Do not access WASAPI directly from UI.
7. All external resources must be disposed correctly.
8. Optimize for low CPU/GPU usage.
9. Sensor sampling and UI rendering rates must be separated.
10. The program must survive display hot-plug.
11. Missing sensors must not crash the application.
12. Use structured logging.
13. Avoid unnecessary dependencies.
14. Do not over-engineer Alpha features.
15. Keep future custom hardware support possible.
