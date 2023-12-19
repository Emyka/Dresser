﻿using CriticalCommonLib;
using CriticalCommonLib.Enums;
using CriticalCommonLib.Extensions;

using Dalamud.Utility;

using Dresser.Extensions;
using Dresser.Logic;
using Dresser.Services;
using Dresser.Structs.Actor;
using Dresser.Windows;

using Lumina.Excel.GeneratedSheets;

using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

using static Dresser.Services.Storage;

using CriticalInventoryItem = CriticalCommonLib.Models.InventoryItem;

namespace Dresser.Structs.Dresser {
	public class InventoryItem : CriticalInventoryItem {

		public string? ModName = null;
		public string? ModDirectory = null;
		public string? ModModelPath = null;
		public string? ModAuthor = null;
		public string? ModVersion = null;
		public string? ModWebsite = null;
		public string? ModIconPath = null;
		public uint QuantityNeeded = 1;


		public InventoryItem(InventoryItem inventoryItem) : base(inventoryItem) {
			if(inventoryItem.IsModded()) PluginLog.Warning($"B Copy InventoryItem {inventoryItem.ModDirectory}");

			this.ModName = inventoryItem.ModName;
			this.ModDirectory = inventoryItem.ModDirectory;
			this.ModModelPath = inventoryItem.ModModelPath;
			if (inventoryItem.IsModded()) PluginLog.Warning($"A Copy InventoryItem {this.ModDirectory}");
		}

		public InventoryItem(InventoryType container, short slot, uint itemId, uint quantity, ushort spiritbond, ushort condition, FFXIVClientStructs.FFXIV.Client.Game.InventoryItem.ItemFlags flags, ushort materia0, ushort materia1, ushort materia2, ushort materia3, ushort materia4, byte materiaLevel0, byte materiaLevel1, byte materiaLevel2, byte materiaLevel3, byte materiaLevel4, byte stain, uint glamourId) : base(container, slot, itemId, quantity, spiritbond, condition, flags, materia0, materia1, materia2, materia3, materia4, materiaLevel0, materiaLevel1, materiaLevel2, materiaLevel3, materiaLevel4, stain, glamourId) { }
		public InventoryItem(InventoryType inventoryType, uint itemId) : this(inventoryType, 0, itemId, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0) {
			this.SortedContainer = inventoryType;
		}
		public InventoryItem(InventoryType inventoryType, uint itemId, string modName, string modDirectory, string modModelPath) : this(inventoryType, itemId) {
			this.ModName = modName;
			this.ModDirectory = modDirectory;
			this.ModModelPath = modModelPath;
		}


		[JsonConstructor]
		public InventoryItem() {

		}


		public static CriticalInventoryItem ToCritical(InventoryItem item) {
			return new CriticalInventoryItem(item);
		}
		public static InventoryItem FromCritical(CriticalInventoryItem item) {
			return new InventoryItem { Container = item.Container, Slot = item.Slot, ItemId = item.ItemId, Quantity = item.Quantity, Spiritbond = item.Spiritbond, Condition = item.Condition, Flags = item.Flags, Materia0 = item.Materia0, Materia1 = item.Materia1, Materia2 = item.Materia2, Materia3 = item.Materia3, Materia4 = item.Materia4, MateriaLevel0 = item.MateriaLevel0, MateriaLevel1 = item.MateriaLevel1, MateriaLevel2 = item.MateriaLevel2, MateriaLevel3 = item.MateriaLevel3, MateriaLevel4 = item.MateriaLevel4, Stain = item.Stain, GlamourId = item.GlamourId, SortedContainer = item.SortedContainer, SortedCategory = item.SortedCategory, SortedSlotIndex = item.SortedSlotIndex, RetainerId = item.RetainerId, RetainerMarketPrice = item.RetainerMarketPrice, GearSets = item.GearSets, };
		}
		public static InventoryItem Zero => new InventoryItem();
		public bool IsModded() {
			return !this.ModDirectory.IsNullOrWhitespace();
		}

		public bool IsModDifferent(InventoryItem? item2) {
			return this.ModName == item2?.ModName
				&& this.ModDirectory == item2?.ModDirectory
				&& this.ModModelPath == item2?.ModModelPath;
		}


		public ItemEquip ToItemEquip() {
			var itemModelMain = this.Item.ModelMainItemModel();

			return new() {
				Id = itemModelMain.Id,
				Variant = (byte)itemModelMain.Variant,
				Dye = this.Item.IsDyeable ? this.Stain : (byte)0,
			};
		}
		public WeaponEquip ToWeaponEquip(WeaponIndex index) {

			var itemModel = index == WeaponIndex.MainHand || Item.IsMainModelOnOffhand() ? Item.ModelMainItemModel() : Item.ModelSubItemModel();
			return new() {
				Set = itemModel.Id,
				Base = itemModel.Base,
				Variant = itemModel.Variant,
				Dye = this.Item.IsDyeable ? this.Stain : (byte)0,
			};
		}

		public static InventoryItem? FromItemEquip(ItemEquip? itemEquip, GlamourPlateSlot slot) {
			if (itemEquip == null || itemEquip.Value.Id == 0) return null;
			var dd = FromModelMain(itemEquip.Value.ToModelId(), slot);
			if (dd != null) return new InventoryItem(InventoryType.Bag0, dd.RowId) {
				Stain = itemEquip.Value.Dye
			};
			return null;
		}

		public static InventoryItem? FromWeaponEquip(WeaponEquip? weaponEquip, GlamourPlateSlot slot) {
			if (weaponEquip == null || weaponEquip.Value.Set == 0) return null;

			//PluginLog.Debug($"appearance: Set:{weaponEquip.Value.Set}, Variant:{weaponEquip.Value.Base}, variant:{weaponEquip.Value.Variant}  ==> {weaponEquip.Value.ToModelId()}");
			//uint stoladressId = 39937;
			//var stoladress = Service.ExcelCache.AllItems.Where(item => item.Value.RowId == stoladressId).First().Value;
			//PluginLog.Debug($"manderville wings: {stoladress.RowId} => model:m:{stoladress.ModelMain} s:{stoladress.ModelSub}");


			var dd = slot.ToWeaponIndex() == WeaponIndex.MainHand || weaponEquip.Value.IsMainModelOnOffhand() ? FromModelWeaponMain(weaponEquip.Value.ToModelId(), slot) : FromModelWeaponSub(weaponEquip.Value.ToModelId(), slot);
			if (dd != null) return new InventoryItem(InventoryType.Bag0, dd.RowId) {
				Stain = weaponEquip.Value.Dye
			};
			return null;
		}
		public static CriticalCommonLib.Sheets.ItemEx? FromModelMain(ulong model, GlamourPlateSlot slot) {

			var equipSlotCategory = slot.ToEquipSlotCategoryByte();
			var ddddd = Service.ExcelCache.AllItems.Where(item => item.Value.ModelMain == model && item.Value.EquipSlotCategory.Row == equipSlotCategory);
			//PluginLog.Debug($"looking for item... {model} => {ddddd.Count()}");
			//foreach((var id,var item) in ddddd) {
			//	PluginLog.Debug($"     found item {id} {item.NameString} {item.ModelMain} {item.ModelSub} => {item.EquipSlotCategory.Row == slot.ToEquipSlotCategoryByte()}");

			//}
			return ddddd.FirstOrDefault().Value;
		}
		public static CriticalCommonLib.Sheets.ItemEx? FromModelWeaponMain(ulong model, GlamourPlateSlot slot) {

			//var equipSlotCategory = slot.ToEquipSlotCategoryByte();
			var ddddd = Service.ExcelCache.AllItems.Where(item => item.Value.ModelMain == model);
			//PluginLog.Debug($"looking for item... {model} => {ddddd.Count()}");
			//foreach ((var id, var item) in ddddd) {
			//	PluginLog.Debug($"     found item {id} {item.NameString} {item.ModelMain} {item.ModelSub} => {item.EquipSlotCategory.Row == slot.ToEquipSlotCategoryByte()}");

			//}
			return ddddd.FirstOrDefault().Value;
		}
		public static CriticalCommonLib.Sheets.ItemEx? FromModelWeaponSub(ulong model, GlamourPlateSlot slot) {
			return Service.ExcelCache.AllItems.Where(item => item.Value.ModelSub == model).FirstOrDefault().Value;
		}


		public WeaponEquip ToWeaponEquipMain()
			=> ToWeaponEquip(WeaponIndex.MainHand);
		public WeaponEquip ToWeaponEquipSub()
			=> ToWeaponEquip(WeaponIndex.OffHand);
		public IEnumerable<InventoryItem> GetDyesInInventories() {
			var stainTransient = Service.ExcelCache.GetSheet<StainTransient>().FirstOrDefault(st => st.RowId == this.Stain);

			var inventories = PluginServices.AllaganTools.GetItemsLocalCharsRetainers(true);
			var foundDyes = inventories.SelectMany(ip => ip.Value.Where(v => v.ItemId == stainTransient?.Item1.Value?.RowId || v.ItemId == stainTransient?.Item2.Value?.RowId)).Where(i=>i.ItemId != 0);

			if(!foundDyes.Any()) {
				var defaultStainRowId = stainTransient?.Item1.Value?.RowId;
				if(defaultStainRowId != null) {
					var unobtainedDye = new InventoryItem((InventoryType)InventoryTypeExtra.AllItems, (uint)defaultStainRowId);

					foundDyes = new List<InventoryItem>() { unobtainedDye };
				}
			}
			//if(excludeBags)
			//	return foundDyes.Where(i=>i.SortedCategory != CriticalCommonLib.Models.InventoryCategory.CharacterBags);

			return foundDyes.Select(i=>i.Copy()!);
		}
		public bool IsNotInBlackList() {
			if (!IsModded()) return true;
			if (ConfigurationManager.Config.PenumbraModsBlacklist.Any(m => m.Path == this.ModDirectory))
				return false;
			return true;
		}

		public bool IsInGearBrowserSelectedSlot() {
			var itemSlot = this.Item.GlamourPlateSlot();
			//PluginLog.Debug($"IsInGearBrowserSelectedSlot: {itemSlot}");
			return itemSlot == GearBrowser.SelectedSlot || (itemSlot == GlamourPlateSlot.RightRing && GearBrowser.SelectedSlot == GlamourPlateSlot.LeftRing);
		}


	}
}
