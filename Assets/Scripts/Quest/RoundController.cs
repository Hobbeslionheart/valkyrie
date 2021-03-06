﻿using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// This class controls the progression of activations and events
public class RoundController {

    // A hero has finished their turn
    virtual public void HeroActivated()
    {
        Game game = Game.Get();
        // Check if all heros have finished
        bool herosActivated = true;
        foreach (Quest.Hero h in game.quest.heroes)
        {
            if (!h.activated && h.heroData != null)
                herosActivated = false;
        }

        // activate a monster group (returns if all activated, does nothing if none left)
        bool monstersActivated = ActivateMonster();

        // If everyone has finished move to next round
        if (monstersActivated && herosActivated)
        {
            EndRound();
        }
    }

    // Finish the other half of monster activation
    public void ParticalActivationComplete(Quest.Monster m)
    {
        // Start the other half of the activation
        new ActivateDialog(m, m.minionStarted);
        m.minionStarted = true;
        m.masterStarted = true;
    }

    // A monster has activated, work out what to do next
    virtual public void MonsterActivated()
    {
        Game game = Game.Get();

        // Check for any partial monster activations
        foreach (Quest.Monster m in game.quest.monsters)
        {
            if (m.minionStarted ^ m.masterStarted)
            {
                // Half activated group, complete then return;
                ParticalActivationComplete(m);
                return;
            }

            // If both started then it is complete
            if (m.minionStarted && m.masterStarted)
            {
                m.activated = true;
            }
        }

        // Full activation, update display
        game.monsterCanvas.UpdateStatus();

        // Check if all heros have finished
        bool herosActivated = true;
        foreach (Quest.Hero h in game.quest.heroes)
        {
            if (!h.activated && h.heroData != null)
                herosActivated = false;
        }

        // If there no heros left activate another monster
        if(herosActivated)
        {
            if (ActivateMonster())
            {
                // Evenyone has finished, move to next round
                EndRound();
            }
        }
    }

    // Activate a monster (if any left) and return true if all monsters activated
    virtual public bool ActivateMonster()
    {
        Game game = Game.Get();

        List<int> notActivated = new List<int>();
        // Get the index of all monsters that haven't activated
        for (int i = 0; i < game.quest.monsters.Count; i++)
        {
            if (!game.quest.monsters[i].activated)
                notActivated.Add(i);
        }

        // If no monsters are found return true
        if (notActivated.Count == 0)
            return true;

        // Find a random unactivated monster
        Quest.Monster toActivate = game.quest.monsters[notActivated[Random.Range(0, notActivated.Count)]];

        return ActivateMonster(toActivate);
    }

    // Activate a monster
    virtual public bool ActivateMonster(Quest.Monster m)
    {
        List<ActivationData> adList = new List<ActivationData>();
        Game game = Game.Get();

        bool customActivations = false;
        MonsterData md = m.monsterData;

        // Find out of this monster is quest specific
        QuestMonster qm = md as QuestMonster;
        if (qm != null)
        {
            // Get the base monster type
            if (game.cd.monsters.ContainsKey(qm.derivedType))
            {
                md = game.cd.monsters[qm.derivedType];
            }
            // Determine if the monster has quest specific activations
            customActivations = !qm.useMonsterTypeActivations;
        }

        // A monster with quest specific activations
        if (customActivations)
        {
            if (!qm.useMonsterTypeActivations)
            {
                adList = new List<ActivationData>();
                // Get all custom activations
                foreach (string s in qm.activations)
                {
                    // Find the activation in quest data
                    if (game.quest.qd.components.ContainsKey("Activation" + s))
                    {
                        adList.Add(new QuestActivation(game.quest.qd.components["Activation" + s] as QuestData.Activation));
                    }
                    // Otherwise look for the activation in contend data
                    else if (game.cd.activations.ContainsKey("MonsterActivation" + s))
                    {
                        adList.Add(game.cd.activations["MonsterActivation" + s]);
                    }
                    else // Invalid activation
                    {
                        ValkyrieDebug.Log("Warning: Unable to find activation: " + s + " for monster type: " + m.monsterData.sectionName);
                    }
                }
            }
        }
        else // Content Data activations only
        {
            // Find all possible activations
            foreach (KeyValuePair<string, ActivationData> kv in game.cd.activations)
            {
                // Is this activation for this monster type? (replace "Monster" with "MonsterActivation", ignore specific variety)
                if (kv.Key.IndexOf("MonsterActivation" + md.sectionName.Substring("Monster".Length)) == 0)
                {
                    adList.Add(kv.Value);
                }
            }
            // Search for additional common activations
            foreach (string s in md.activations)
            {
                if (game.cd.activations.ContainsKey("MonsterActivation" + s))
                {
                    adList.Add(game.cd.activations["MonsterActivation" + s]);
                }
                else
                {
                    ValkyrieDebug.Log("Warning: Unable to find activation: " + s + " for monster type: " + md.sectionName);
                }
            }
        }

        // Check for no activations
        if (adList.Count == 0)
        {
            ValkyrieDebug.Log("Error: Unable to find any activation data for monster type: " + md.name);
            Application.Quit();
        }

        // No current activation
        if (m.currentActivation == null)
        {
            // Pick a random activation
            ActivationData activation = adList[Random.Range(0, adList.Count)];
            m.NewActivation(activation);
        }

        // MoM has a special activation
        if (game.gameType is MoMGameType)
        {
            m.masterStarted = true;
            new ActivateDialogMoM(m);
            return false;
        }

        // If no minion activation just do master
        if (m.currentActivation.ad.minionActions == null)
        {
            m.minionStarted = true;
            m.masterStarted = true;
            new ActivateDialog(m, true);
            return false;
        }

        // If no master activation just do minion
        if (m.currentActivation.ad.masterActions == null)
        {
            m.minionStarted = true;
            m.masterStarted = true;
            new ActivateDialog(m, false);
            return false;
        }

        // Random pick Minion or master (both available)
        m.minionStarted = Random.Range(0, 2) == 0;

        // If order specificed then use that instead
        if(m.currentActivation.ad.masterFirst)
        {
            m.minionStarted = false;
        }
        if (m.currentActivation.ad.minionFirst)
        {
            m.minionStarted = true;
        }

        // Master is opposite of minion as this is the first activation
        m.masterStarted = !m.minionStarted;

        // Create activation window
        new ActivateDialog(m, m.masterStarted);

        // More groups unactivated
        return false;
    }

    // All activations finished, start end of round
    public void EndRound()
    {
        Game game = Game.Get();

        // Queue end of all round events
        game.quest.eManager.EventTriggerType("EndRound", false);
        // Queue end of this round events
        game.quest.eManager.EventTriggerType("EndRound" + game.quest.round, false);

        if (game.quest.flags.Contains("#eliminatedprev"))
        {
            game.quest.eManager.EventTriggerType("Eliminated", false);
        }
        if (game.quest.flags.Contains("#eliminated"))
        {
            game.quest.flags.Add("#eliminatedprev");
        }

        // This will cause the end of the round if nothing was added
        game.quest.eManager.TriggerEvent();
    }

    // Check if ready for new round
    public virtual void CheckNewRound()
    {

        Game game = Game.Get();

        // Is there an active event?
        if (game.quest.eManager.currentEvent != null)
            return;

        // Are there queued events?
        if (game.quest.eManager.eventStack.Count > 0)
            return;

        // Check if all heros have finished
        foreach (Quest.Hero h in game.quest.heroes)
        {
            if (!h.activated && h.heroData != null) return;
        }

        // Check if all monsters have finished
        foreach (Quest.Monster m in game.quest.monsters)
        {
            if (!m.activated) return;
        }

        // Check for delayed events
        foreach (QuestData.Event.DelayedEvent de in game.quest.delayedEvents)
        {
            if (de.delay == game.quest.round)
            {
                // Trigger delayed event
                game.quest.delayedEvents.Remove(de);
                game.quest.eManager.QueueEvent(de.eventName);
                return;
            }
        }

        // Check if we are due for a minor peril
        if (!game.quest.minorPeril && game.quest.qd.quest.minorPeril <= game.quest.round)
        {
            game.quest.eManager.RaisePeril(PerilData.PerilType.minor);
            game.quest.minorPeril = true;
            return;
        }

        // Check if we are due for a major peril
        if (!game.quest.majorPeril && game.quest.qd.quest.majorPeril <= game.quest.round)
        {
            game.quest.eManager.RaisePeril(PerilData.PerilType.major);
            game.quest.majorPeril = true;
            return;
        }

        // Check if we are due for a deadly peril
        if (!game.quest.deadlyPeril && game.quest.qd.quest.deadlyPeril <= game.quest.round)
        {
            game.quest.eManager.RaisePeril(PerilData.PerilType.deadly);
            game.quest.deadlyPeril = true;
            return;
        }

        // Clean up for next round
        // Clear hero activations
        foreach (Quest.Hero h in game.quest.heroes)
        {
            h.activated = false;
        }
        // Clear monster activations
        foreach (Quest.Monster m in game.quest.monsters)
        {
            m.activated = false;
            m.minionStarted = false;
            m.masterStarted = false;
            m.currentActivation = null;
        }

        // Increment the round
        game.quest.round++;
        game.quest.threat += 1;

        // Update monster and hero display
        game.monsterCanvas.UpdateStatus();
        game.heroCanvas.UpdateStatus();
    }
}
