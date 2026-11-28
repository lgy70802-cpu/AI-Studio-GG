import { PLUGIN_CAPABILITY_REGISTRY } from "./editorCatalog";
import type { PluginItem } from "./themes";

export const COMPONENT_PLUGIN_IDS = new Set([
  "plg-clock",
  "plg-telemetry",
  "plg-audio",
  "plg-pomodoro",
  "plg-weather",
  "plg-sticky",
]);

export function pluginCapability(plugin: PluginItem) {
  const aliases: Record<string, string> = {
    "plg-craft-aquarium": "desktop-aquarium",
    "plg-craft-nixie": "nixie-clock",
    "plg-craft-bonsai": "zen-bonsai",
  };
  return PLUGIN_CAPABILITY_REGISTRY.find(
    (item) =>
      item.name === plugin.name ||
      item.key === aliases[plugin.id] ||
      item.key === plugin.id.replace("plg-craft-", ""),
  );
}

export function canOutputPlugin(plugin: PluginItem) {
  return [
    "desktop-aquarium",
    "nixie-clock",
    "flip-clock",
    "campfire",
    "zen-bonsai",
    "cyber-pet",
  ].includes(pluginCapability(plugin)?.key ?? "");
}
