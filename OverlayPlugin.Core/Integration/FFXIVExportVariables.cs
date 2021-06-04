using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Advanced_Combat_Tracker;

namespace RainbowMage.OverlayPlugin
{
    class FFXIVExportVariables
    {
        static string outH = CombatantData.DamageTypeDataOutgoingHealing;

        public static void Init()
        {
            // TODO: Profile and optimize if necessary.

            // The code below was taken under MIT license from https://github.com/ZCube/ACTWebSocket/blob/master/ACTWebSocket.Core/Functions/OverlayACTWork.cs.
            // Copyright (c) 2016 ZCube

            if (!CombatantData.ExportVariables.ContainsKey("overHeal"))
            {
                CombatantData.ExportVariables.Add
                (
                    "overHeal",
                    new CombatantData.TextExportFormatter
                    (
                        "overHeal",
                        "Overheal",
                        "Amount of healing that made flood over 100% of health.",
                        (Data, ExtraFormat) =>
                        { 
                            if (!Data.Items[outH].Items.TryGetValue("All", out AttackType attack))
                            {
                                return "0";
                            }

                            long sum = 0;
                            var swings = attack.Items;
                            for (var i = 0; i < swings.Count; i++)
                            {
                                if (swings[i].Tags.TryGetValue("overheal", out object value))
                                {
                                    sum += Convert.ToInt64(value);
                                }
                            }

                            return sum.ToString();
                        }
                    )
                );
            }

            if (!CombatantData.ExportVariables.ContainsKey("damageShield"))
            {
                CombatantData.ExportVariables.Add
                (
                    "damageShield",
                    new CombatantData.TextExportFormatter
                    (
                        "damageShield",
                        "Damage Shield",
                        "Damage blocked by Shield skills of healer.",
                        (Data, ExtraFormat) =>
                        {
                            if (!Data.Items[outH].Items.TryGetValue("All", out AttackType attack))
                            {
                                return "0";
                            }

                            long sum = 0;
                            var swings = attack.Items;
                            for (var i = 0; i < swings.Count; i++)
                            {
                                if (swings[i].DamageType == "DamageShield")
                                {
                                    sum += swings[i].Damage;
                                }
                            }

                            return sum.ToString();
                        }
                    )
                );
            }

            if (!CombatantData.ExportVariables.ContainsKey("absorbHeal"))
            {
                CombatantData.ExportVariables.Add
                (
                    "absorbHeal",
                    new CombatantData.TextExportFormatter
                    (
                        "absorbHeal",
                        "Healed by Absorbing",
                        "Amount of heal, done by absorbing.",
                        (Data, ExtraFormat) =>
                        {
                            if (!Data.Items[outH].Items.TryGetValue("All", out AttackType attack))
                            {
                                return "0";
                            }

                            long sum = 0;
                            var swings = attack.Items;
                            for (var i = 0; i < swings.Count; i++)
                            {
                                if (swings[i].DamageType == "Absorb")
                                {
                                    sum += swings[i].Damage;
                                }
                            }

                            return sum.ToString();
                        }
                    )
                );
            }
        }
    }
}
