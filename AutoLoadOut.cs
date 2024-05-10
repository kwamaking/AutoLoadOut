using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Ext.AutoKit;
using Oxide.Ext.AutoKit.Messages;
using Oxide.Ext.AutoKit.Models;
using Oxide.Ext.AutoKit.Settings;

namespace Oxide.Plugins
{
    [Info( "AutoLoadOut", "kwamaking", "1.0.0" )]
    [Description( "Save your inventory loadouts." )]

    class AutoLoadOut : RustPlugin
    {
        private const string UsePermission = "autoloadout.use";
        private AutoKit<LoadOutItem> autoKit { get; set; }
        private List<PlayerInventory.Type> playerInventoryTypes { get; set; }
        private AutoLoadOutMessages messages { get; set; }

        #region Oxide Hooks

        void OnServerInitialized()
        {
            playerInventoryTypes = new List<PlayerInventory.Type> { PlayerInventory.Type.Belt, PlayerInventory.Type.Main, PlayerInventory.Type.Wear };
            permission.RegisterPermission( UsePermission, this );
            cmd.AddChatCommand( "alo", this, "AutoLoadOutCommand" );
            messages = new AutoLoadOutMessages();
            autoKit = new AutoKit<LoadOutItem>( messages, new AutoKitSettings( pluginName: this.Name, iconId: 76561198955675901) );
        }

        void OnServerSave()
        {
            autoKit.Save();
        }

        void Unload()
        {
            autoKit.Save();
        }

        private void OnKitRedeemed( BasePlayer player, string kitName )
        {
            autoKit.With( player, ( action ) => action.WithKit( kitName ).MaybeApply( Apply ) );
        }

        private void OnKitSaved( BasePlayer player, string kitName )
        {
            autoKit.With( player, ( action ) => action.WithNewKit( kitName ).MaybeSave( Save ).Notify() );
            autoKit.Save();
        }

        private void OnKitRemoved( BasePlayer player, string kitName )
        {
            autoKit.With( player, ( action ) => action.WithKit( kitName ).MaybeRemove() );
        }

        #endregion

        #region Commands

        [ChatCommand( "autoloadout" )]
        void AutoLoadOutCommand( BasePlayer player, string command, string[] args )
        {
            try
            {
                if ( !permission.UserHasPermission( player.UserIDString, UsePermission ) )
                {
                    autoKit.With( player, ( action ) => action.ToNonDestructive().WithNotification( messages.noPermission ).ToNotify().Notify() );
                    return;
                }
                var run = args.ElementAtOrDefault( 0 ) ?? "help";
                var kitName = args.ElementAtOrDefault( 1 ) ?? run;
                switch ( run )
                {
                    case "save":
                        autoKit.With( player, ( action ) => action.WithNewKit( kitName ).Save( Save ).Notify() );
                        autoKit.Save();
                        break;
                    case "help":
                        autoKit.With( player, ( action ) => action.ToNonDestructive().WithNotification( messages.help ).ToNotify().Notify() );
                        break;
                    case "list":
                        autoKit.With( player, ( action ) => action.ToNonDestructive().ListToNotify().Notify() );
                        break;
                    case "remove":
                        autoKit.With( player, ( action ) => action.WithKit( kitName ).Remove().Notify() );
                        break;
                    default:
                        autoKit.With( player, ( action ) => action.WithKit( kitName ).Apply( Apply ).Notify() );
                        break;
                }
            }

            catch ( Exception e )
            {
                Puts( $"Failed to run AutoLoadOut: {e.Message}" );
            }
        }

        #endregion

        #region AutoLoadOut

        private Kit<LoadOutItem> Save( BasePlayer player, Kit<LoadOutItem> kit )
        {
            playerInventoryTypes.ForEach( inventoryType =>
            {
                player.inventory.GetContainer( inventoryType ).itemList.ForEach( item =>
                  {
                      kit.items.Add( new LoadOutItem
                      {
                          id = item.info.itemid,
                          position = item.position,
                          inventoryType = inventoryType,
                          isBeingHeld = player.GetActiveItem()?.info?.itemid == item.info.itemid,
                          attachments = item.contents?.itemList?.ConvertAll( attachment =>
                          {
                              return new LoadOutItem
                              {
                                  id = attachment.info.itemid,
                              };
                          } ) ?? new List<LoadOutItem>()
                      } );
                  } );
            } );

            return kit;
        }

        private void Apply( BasePlayer player, Kit<LoadOutItem> kit )
        {
            if ( null == kit ) return;

            kit.items.ForEach( kitItem =>
            {
                var playerInventory = GetPlayerInventory( player );
                var foundItem = playerInventory.Find( item => item.info.itemid == kitItem.id );
                foundItem?.MoveToContainer( player.inventory.GetContainer( kitItem.inventoryType ) );
                foundItem?.MoveToContainer( player.inventory.GetContainer( kitItem.inventoryType ), kitItem.position );
                kitItem.attachments?.ForEach( attachment =>
                {
                    var foundAttachment = playerInventory.Find( p => p.info.itemid == attachment.id );
                    if ( null != foundAttachment )
                    {
                        var foundItemForAttachment = playerInventory
                        .FindAll( item => item.info.itemid == kitItem.id )
                        .Find( item => !item.contents?.itemList?.Any( i => i.info.itemid == foundAttachment.info.itemid ) ?? true );

                        if ( null != foundItemForAttachment )
                        {
                            foundItemForAttachment.contents?.AddItem( foundAttachment.info, 1 );
                            foundItemForAttachment.parent?.itemList?.Remove( foundAttachment );
                            foundAttachment.RemoveFromContainer();
                            foundAttachment.RemoveFromWorld();
                        }
                    }
                } );
            } );
        }


        private List<Item> GetPlayerInventory( BasePlayer player ) => player.inventory.AllItems().ToList();

        #endregion

        #region Configuration Classes

        public class LoadOutItem
        {
            [JsonProperty( "id" )]
            public int id { get; set; }
            [JsonProperty( "position" )]
            public int position { get; set; }
            [JsonProperty( "isBeingHeld" )]
            public bool isBeingHeld { get; set; }
            [JsonProperty( "attachments" )]
            public List<LoadOutItem> attachments { get; set; } = new List<LoadOutItem>();
            [JsonProperty( "inventoryType" )]
            public PlayerInventory.Type inventoryType { get; set; } = PlayerInventory.Type.Main;
        }

        public class AutoLoadOutMessages : AutoKitMessages
        {
            public string noPermission { get; set; } = "You do not have permission to use AutoLoadOut.";
            public string help { get; set; } =
                 "\n<color=green>/autoloadout <kit></color>. - apply a saved loadout kit to your inventory, \n" +
                "<color=green>/autoloadout save <kit></color> - Save the items in your inventory as a loadout kit, \n" +
                "<color=green>/autoloadout list</color> - List your saved loadout kits, \n" +
                "<color=green>/autoloadout remove <kit></color> - Remove a saved loadout kit, \n" +
                "<color=green>/autoloadout help</color> - To see this message again.\n" +
                "<color=green>/alo</color> Command shortcut.\n ";
        }
        #endregion
    }
}
