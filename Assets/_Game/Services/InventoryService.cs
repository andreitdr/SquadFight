using System.Collections.Generic;
using System.Linq;
using Game.Core;
using Game.Save;

namespace Game.Services
{
    /// <summary>Read-only snapshot of an owned item for UI.</summary>
    public readonly struct ItemInfo
    {
        public readonly string InstanceId;
        public readonly EquipmentTemplateDef Template;
        public readonly Rarity Rarity;
        public readonly string EquippedBy;
        public readonly int SlotIndex;
        public readonly long AcquiredOrder;

        public ItemInfo(EquipmentSave s, EquipmentTemplateDef template)
        {
            InstanceId = s.InstanceId;
            Template = template;
            Rarity = s.Rarity;
            EquippedBy = string.IsNullOrEmpty(s.EquippedByHeroId) ? null : s.EquippedByHeroId;
            SlotIndex = s.EquippedSlotIndex;
            AcquiredOrder = s.AcquiredOrder;
        }

        public bool IsEquipped => EquippedBy != null;
        public EquipmentSlot Slot => Template.Slot;
    }

    /// <summary>Owned equipment instances: add, remove, query, equip/unequip, sell.</summary>
    public sealed partial class InventoryService
    {
        private readonly ServiceContext _ctx;

        public InventoryService(ServiceContext ctx) => _ctx = ctx;

        public int Count => _ctx.State.Data.Equipment.Count;
        public int Limit => _ctx.Db.Rules.InventoryLimit;
        public bool IsFull => Limit > 0 && Count >= Limit;

        public bool Has(string instanceId) => _ctx.State.GetItem(instanceId) != null;

        public ItemInfo? Get(string instanceId)
        {
            var s = _ctx.State.GetItem(instanceId);
            if (s == null || !_ctx.Db.TryGetEquipment(s.TemplateId, out var t)) return null;
            return new ItemInfo(s, t);
        }

        /// <summary>All items whose template still exists in content.</summary>
        public IReadOnlyList<ItemInfo> All()
        {
            var list = new List<ItemInfo>(Count);
            foreach (var s in _ctx.State.Data.Equipment)
                if (_ctx.Db.TryGetEquipment(s.TemplateId, out var t)) list.Add(new ItemInfo(s, t));
            return list;
        }

        public Result AddItem(EquipmentInstance item)
        {
            if (item == null || string.IsNullOrEmpty(item.InstanceId)) return Result.Fail(ErrorCode.InvalidArgument, "item");
            if (!_ctx.Db.TryGetEquipment(item.TemplateId, out _)) return Result.Fail(ErrorCode.NotFound, item.TemplateId);
            if (Has(item.InstanceId)) return Result.Fail(ErrorCode.InvalidArgument, "duplicate instance id " + item.InstanceId);
            if (IsFull) return Result.Fail(ErrorCode.InventoryFull);
            _ctx.State.AddItem(new EquipmentSave
            {
                InstanceId = item.InstanceId,
                TemplateId = item.TemplateId,
                Rarity = item.Rarity,
                RolledStats = item.RolledStats?.ToList(),
                AcquiredOrder = _ctx.State.NextAcquiredOrder(),
            });
            _ctx.Commit();
            _ctx.Emit(new ItemAdded(item.InstanceId));
            return Result.Success;
        }
    }
}
