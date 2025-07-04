
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.Economy;
using VRC.SDK3.Data;
using VRC.SDK3.StringLoading;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;
using static VRC.Core.ApiAvatar;

namespace mahu.games
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class MembersList : UdonSharpBehaviour
    {
        public TextMeshProUGUI supportersText;

        [Tooltip("One or more products to retrieve the owners of.")]
        public UdonProduct[] membershipProducts;

        [Tooltip("Optional. Shown when there are members that own the product, hidden otherwise.")]
        public GameObject titleObject;

        [Tooltip("When true shuffles the players names before display, otherwise displayed in the order returned by VRChat.")]
        public bool shuffle;

        [Tooltip("List of any additional members who should appear in the list but do not have the udon product.")]
        public string[] additionalMembers;

        [NonSerialized]
        public string[] members;

        [NonSerialized]
        public bool loaded;

        private int loadedProductCount;

        void Start()
        {
            loadedProductCount = 0;
            members = new string[0];

            // Update display of product owners with any default members before loading product owners
            _UpdateSupporterDisplay();

            for (int i = 0; i < membershipProducts.Length; i++)
            {
                var product = membershipProducts[i];
                if (product != null)
                {
                    Store.ListProductOwners((IUdonEventReceiver)this, product);
                }
            }
        }

        public override void OnListProductOwners(IProduct product, string[] owners)
        {
            if (product != null)
            {
                Debug.Log($"[mahu] SupportersBoard - Loaded product owners {product.ID} {product.Name} ({owners.Length} owners)");

                int curMembersLength = members.Length;
                var newMembers = new string[owners.Length + curMembersLength];
                Array.Copy(members, newMembers, curMembersLength);
                members = newMembers;

                Array.Copy(owners, 0, members, curMembersLength, owners.Length);

                loadedProductCount++;
            }

            if (loadedProductCount >= membershipProducts.Length)
            {
                loaded = true;
                _UpdateSupporterDisplay();
            }
        }

        public void _UpdateSupporterDisplay()
        {
            var displayMembers = new string[members.Length + additionalMembers.Length];

            if (displayMembers.Length == 0)
            {
                if (Utilities.IsValid(titleObject))
                {
                    titleObject.SetActive(false);
                }

                supportersText.text = "";
                return;
            }

            if (Utilities.IsValid(titleObject))
            {
                titleObject.SetActive(true);
            }

            Array.Copy(additionalMembers, displayMembers, additionalMembers.Length);
            Array.Copy(members, 0, displayMembers, additionalMembers.Length, members.Length);

            if (shuffle)
            {
                for (int i = 0; i < displayMembers.Length - 1; i++)
                {
                    // Patented line of code guaranteed not to crash the game client a small percentage of the time, or your money back!
                    var j = UnityEngine.Random.Range(i, displayMembers.Length);
                    var tmp = displayMembers[i];
                    displayMembers[i] = displayMembers[j];
                    displayMembers[j] = tmp;
                }
            }

            // Wraps the members names in <nobr> </nobr> since otherwise some players names will allow line breaks
            var joinedMemberNames = "<nobr>" + string.Join("</nobr>   <nobr>", displayMembers) + "</nobr>";
            supportersText.text = joinedMemberNames;
        }

        public override void OnPurchaseConfirmed(IProduct product, VRCPlayerApi player, bool newPurchase)
        {
            if (!loaded || !newPurchase)
            {
                return;
            }

            bool isMembershipProduct = false;
            for (int i = 0; i < membershipProducts.Length; i++)
            {
                if (product.ID == membershipProducts[i].ID)
                {
                    isMembershipProduct = true;
                    break;
                }
            }

            if (isMembershipProduct)
            {
                int curMembersLength = members.Length;
                var newMembers = new string[curMembersLength + 1];
                Array.Copy(members, newMembers, curMembersLength);
                members = newMembers;

                members[curMembersLength] = player.displayName;

                _UpdateSupporterDisplay();
            }
        }
    }
}
