
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;

using Dresser.Data;
using Dresser.Windows;
using Dresser.Interop.Hooks;
using Dresser.Interop;
using CriticalCommonLib;
using Dresser.Logic;
using System;
using Dalamud.Game;
using CriticalCommonLib.Models;
using CriticalCommonLib.Services;
using Dalamud.Logging;
using System.Collections.Generic;
using Dalamud.Game.Text.SeStringHandling;
using System.Linq;

namespace Dresser {
	public sealed class Plugin : IDalamudPlugin {
		public string Name => "Dresser";
		private const string CommandName = "/dresser";

		public static Configuration PluginConfiguration => ConfigurationManager.Config;

		public WindowSystem WindowSystem = new("Dresser");

		private static Plugin? PluginInstance = null;
		public static Plugin GetInstance() => PluginInstance!;

		public Plugin(
			[RequiredVersion("1.0")] DalamudPluginInterface pluginInterface,
			[RequiredVersion("1.0")] CommandManager commandManager) {
			PluginInstance = this;
			PluginServices.Init(pluginInterface);



			PluginConfiguration.ConfigurationChanged += ConfigOnConfigurationChanged;
			PluginServices.InventoryMonitor.OnInventoryChanged += InventoryMonitorOnOnInventoryChanged;
			PluginServices.CharacterMonitor.OnCharacterUpdated += CharacterMonitorOnOnCharacterUpdated;
			Service.Framework.Update += FrameworkOnUpdate;

			PluginServices.InventoryMonitor.LoadExistingData(PluginConfiguration.GetSavedInventory());
			PluginServices.CharacterMonitor.LoadExistingRetainers(PluginConfiguration.GetSavedRetainers());







			ImageGuiCrop.Init();
			Gathering.Init();

			Methods.Init();
			Interop.Hooks.AddonListeners.Init();



			WindowSystem.AddWindow(new ConfigWindow(this));
			WindowSystem.AddWindow(new GearBrowser(this));
			WindowSystem.AddWindow(new CurrentGear());


			PluginServices.CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand) {
				HelpMessage = "Open dresser."
			});

			pluginInterface.UiBuilder.Draw += DrawUI;
			pluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;


			EventManager.GearSelectionOpen += OpenDresser;
			EventManager.GearSelectionClose += CloseDresser;

			if (GlamourPlates.IsGlamingAtDresser())
				EventManager.GearSelectionOpen?.Invoke();
		}

		public void Dispose() {
			EventManager.GearSelectionClose?.Invoke();

			this.WindowSystem.RemoveAllWindows();
			PluginServices.CommandManager.RemoveHandler(CommandName);

			PluginServices.GameInterface.AcquiredItemsUpdated -= GameInterfaceOnAcquiredItemsUpdated;
			PluginConfiguration.SavedCharacters = PluginServices.CharacterMonitor.Characters;
			Service.Framework.Update -= FrameworkOnUpdate;
			PluginServices.InventoryMonitor.OnInventoryChanged -= InventoryMonitorOnOnInventoryChanged;
			PluginServices.CharacterMonitor.OnCharacterUpdated -= CharacterMonitorOnOnCharacterUpdated;
			PluginConfiguration.ConfigurationChanged -= ConfigOnConfigurationChanged;


			EventManager.GearSelectionOpen -= OpenDresser;
			EventManager.GearSelectionClose -= CloseDresser;


			Interop.Hooks.AddonListeners.Dispose();
			ImageGuiCrop.Dispose();
			PluginServices.Dispose();
		}

		private void OnCommand(string command, string args) {
			PluginLog.Debug($"{command} {args}");
			switch (args) {
				case "config": DrawConfigUI(); break;
				default:
					// in response to the slash command, just display our main ui
					WindowSystem.GetWindow("Gear Browser")!.IsOpen = true;
					WindowSystem.GetWindow("Current Gear")!.IsOpen = true;
					break;
			}
		}

		private void DrawUI() {
			this.WindowSystem.Draw();
		}

		public void DrawConfigUI() {
			WindowSystem.GetWindow("Dresser Settings")!.IsOpen = true;
		}
		public void ToggleDresser() {
			WindowSystem.GetWindow("Current Gear")!.IsOpen = !IsDresserVisible();
			WindowSystem.GetWindow("Gear Browser")!.IsOpen = !IsDresserVisible();
		}
		public void OpenDresser() {
			//PluginLog.Debug($"OpenDresser");
			WindowSystem.GetWindow("Current Gear")!.IsOpen = true;
			WindowSystem.GetWindow("Gear Browser")!.IsOpen = true;
		}
		public void CloseDresser() {
			//PluginLog.Debug($"CloseDresser");
			WindowSystem.GetWindow("Current Gear")!.IsOpen = false;
			WindowSystem.GetWindow("Gear Browser")!.IsOpen = false;
		}
		public bool IsDresserVisible()
			=> WindowSystem.GetWindow("Current Gear")!.IsOpen;







		// Inventory tools save inventories
		private DateTime? _nextSaveTime = null;
		public void ClearAutoSave() {
			_nextSaveTime = null;
		}
		public DateTime? NextSaveTime => _nextSaveTime;

		private void FrameworkOnUpdate(Framework framework) {
			if (PluginConfiguration.AutoSave) {
				if (NextSaveTime == null && PluginConfiguration.AutoSaveMinutes != 0) {
					_nextSaveTime = DateTime.Now.AddMinutes(PluginConfiguration.AutoSaveMinutes);
				} else {
					if (DateTime.Now >= NextSaveTime) {
						//PluginLog.Debug("===============SAVING INV NOW==============");
						_nextSaveTime = null;
						ConfigurationManager.SaveAsync();
					}
				}
			}

		}
		private void ConfigOnConfigurationChanged() {
			ConfigurationManager.Save();
		}


		private Dictionary<ulong, List<Payload>> _cachedTooltipLines = new();
		private bool _clearCachedLines = false;


		private Dictionary<uint, InventoryMonitor.ItemChangesItem> _recentlyAddedSeen = new();
		private void InventoryMonitorOnOnInventoryChanged(Dictionary<ulong, Dictionary<InventoryCategory, List<InventoryItem>>> inventories, InventoryMonitor.ItemChanges itemChanges) {
			PluginLog.Verbose($"PluginLogic: Inventory changed, saving to config.");
			PluginLog.Debug($"====== RECORD UPDATE {inventories.Count + itemChanges.NewItems.Count + itemChanges.RemovedItems.Count}");
			_clearCachedLines = true;
			PluginConfiguration.SavedInventories = inventories;
			PluginLog.Debug($"====== inv updated {PluginConfiguration.SavedInventories.Select(t=>t.Value.Count).Sum()}");

			foreach (var item in itemChanges.NewItems) {
				if (_recentlyAddedSeen.ContainsKey(item.ItemId)) {
					_recentlyAddedSeen.Remove(item.ItemId);
				}
				_recentlyAddedSeen.Add(item.ItemId, item);
			}
		}

		private void CharacterMonitorOnOnCharacterUpdated(Character? character) {
			PluginLog.Debug($"====== RECORD CHAR UPDATE");

			if (character != null) {
				if (PluginConfiguration.AcquiredItems.ContainsKey(character.CharacterId)) {
					PluginServices.GameInterface.AcquiredItems = PluginConfiguration.AcquiredItems[character.CharacterId];
				}
			} else {
				PluginServices.GameInterface.AcquiredItems = new HashSet<uint>();
			}
		}
		private void GameInterfaceOnAcquiredItemsUpdated() {
			PluginLog.Debug($"====== RECORD ITEM ACQUIRE UPDATE");

			var activeCharacter = PluginServices.CharacterMonitor.ActiveCharacter;
			if (activeCharacter != 0) {
				PluginConfiguration.AcquiredItems[activeCharacter] = PluginServices.GameInterface.AcquiredItems;
			}
		}

	}
}
