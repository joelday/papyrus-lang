export class StarfieldConstants {
    public static parseOutReflectionInfo(valueStr: string) {
        /**
         * Values for compound variables are returned like this:
         *
         * [{FormClass} <{reflection_data}>]
         *
         * the reflection_data is a short string that follows the following formats:
         *  form:
         *    %s (%08X)
         *    <nullptr form> (%08X)
         * //example: [Armor <Clothes_Miner_UtilitySuit (0001D1E7)>]
         *
         *  topicinfo - doesn't look like you can get this via the debugger
         *    topic info %08X on <nullptr quest>
         *    topic info %08X on quest %s (%08X)
         *
         *  alias:
         *    alias %s on quest %s (%08X)
         *    alias %s on <nullptr quest> (%08X)
         *    <nullptr alias> (%hu) on %squest %s (%08X)
         *    <nullptr alias> (%hu) on <nullptr quest> (%08X)
         *  example: [mq101playeraliasscript <alias Player on quest MQ101 (00003448)>]
         *
         *  inventoryItem:
         *    Item %hu in <nullptr container> (%08X)
         *    Item %hu in container %s (%08X)
         *  example: [Weapon <Item 21 in container Thing (00000014)>]
         *
         *  activeEffect:
         *    Active effect %hu on <nullptr actor> (%08X)
         *    Active effect %hu on %s (%08X)
         *  example: [MagicEffect <Active effect 1 on Actor (00005251)>]
         *
         *  inputEnableLayer:
         *    Input enable layer <no name> (%08X)
         *    Input enable layer %s (%08X)
         *    Invalid input enable layer (%08X)
         *  layerId is the formId of the layer `(XXXXXXXX)`
         *  example: [InputEvent <Input enable layer 1 on Player (00000007)>]
         *
         */
        /*                             v yes that space is supposed to be there */
        const re = /\[([\w\d_]+) <(.*)? \(([A-F\d]{8})\)>\]/g;
        const match = re.exec(valueStr);
        if (match?.length == 4) {
            const baseForm = match?.[1];
            const rInfo = match?.[2];
            const formId = parseInt(match?.[3], 16);

            if (!rInfo || rInfo.length == 0) {
                return {
                    baseForm: baseForm,
                    root: {
                        type: 'value',
                        valueType: 'form',
                        formId: formId,
                    },
                };
            }
            if (rInfo.includes('alias') && rInfo.includes('on quest')) {
                const parts = rInfo.replace('alias ', '').split(' on quest ');
                return {
                    baseForm: baseForm,
                    root: {
                        type: 'value',
                        valueType: 'alias',
                        aliasName: parts[0],
                        questName: parts[1],
                        questFormId: formId,
                    },
                };
            } else if (rInfo.startsWith('Item ') && rInfo.includes(' in container ')) {
                const parts = rInfo.replace('Item ', '').split(' in container ');
                return {
                    baseForm: baseForm,
                    root: {
                        type: 'value',
                        valueType: 'inventoryItem',
                        uniqueId: parseInt(parts[0]),
                        containerName: parts[1],
                        containerFormId: formId,
                    },
                };
            } else if (rInfo.startsWith('Active effect ')) {
                const parts = rInfo.replace('Active effect ', '').split(' on ');
                return {
                    baseForm: baseForm,
                    root: {
                        type: 'value',
                        valueType: 'activeEffect',
                        effectId: parseInt(parts[0]),
                        targetName: parts[1],
                        targetFormId: formId,
                    },
                };
            } else if (rInfo.startsWith('Input enable layer ')) {
                return {
                    baseForm: baseForm,
                    root: {
                        type: 'value',
                        valueType: 'inputEnableLayer',
                        layerId: formId,
                    },
                };
            } else if (!rInfo.includes(' ')) {
                return {
                    baseForm: baseForm,
                    root: {
                        type: 'value',
                        valueType: 'form',
                        formId: formId,
                        formName: rInfo,
                    },
                };
            } else {
                return {
                    baseForm: baseForm,
                    root: {
                        type: 'value',
                        valueType: 'form',
                        formId: formId,
                    },
                };
            }
        }
        return undefined;
    }

    public static readonly NATIVE_FORMS = [
        'Action',
        'Activator',
        'ActiveMagicEffect',
        'Actor',
        'ActorBase',
        'ActorValue',
        'AffinityEvent',
        'Alias',
        'Ammo',
        'Armor',
        'AssociationType',
        'Book',
        'CameraShot',
        'Cell',
        'Challenge',
        'Class',
        'CombatStyle',
        'ConditionForm',
        'ConstructibleObject',
        'Container',
        'Curve',
        'Debug',
        'Door',
        'EffectShader',
        'Enchantment',
        'Explosion',
        'Faction',
        'Flora',
        'Form',
        'FormList',
        'Furniture',
        'GlobalVariable',
        'Hazard',
        'HeadPart',
        'Idle',
        'IdleMarker',
        'ImageSpaceModifier',
        'ImpactDataSet',
        'Ingredient',
        'InputEnableLayer',
        'InstanceNamingRules',
        'Key',
        'Keyword',
        'LegendaryItem',
        'LeveledActor',
        'LeveledItem',
        'LeveledSpaceshipBase',
        'LeveledSpell',
        'Light',
        'Location',
        'LocationAlias',
        'LocationRefType',
        'MagicEffect',
        'Message',
        'MiscObject',
        'MovableStatic',
        'MusicType',
        'Note',
        'ObjectMod',
        'ObjectReference',
        'Outfit',
        'Package',
        'PackIn',
        'Perk',
        'Planet',
        'Potion',
        'Projectile',
        'Quest',
        'Race',
        'RefCollectionAlias',
        'ReferenceAlias',
        'ResearchProject',
        'Resource',
        'Scene',
        'Scroll',
        'ShaderParticleGeometry',
        'Shout',
        'SoulGem',
        'SpaceshipBase',
        'SpaceshipReference',
        'Spell',
        'Static',
        'TalkingActivator',
        'Terminal',
        'TerminalMenu',
        'TextureSet',
        'Topic',
        'TopicInfo',
        'VisualEffect',
        'VoiceType',
        'Weapon',
        'Weather',
        'WordOfPower',
        'WorldSpace',
        'WwiseEvent',
        // Non Form Natives
        'SpeechChallengeObject', // not technically a form according to the game? but still a form according to papyrus...
        'Game',
        'Math',
        'ScriptObject', // This is the form that every Papyrus object inherits from
        'Utility',
    ];

    public static readonly checkNativeForm = (form: string): boolean => {
        return StarfieldConstants.NATIVE_FORMS.includes(form);
    };
}
