Scriptname BoS100Fight_GhoulAdditionTriggerScript extends ObjectReference Const
{Trigger used to add additional ghouls (ghouls that run in from College Square, etc.) to the BoS100Fight AllGhouls array.}

BoS100FightMonitor property BoS100Fight Auto Const
Race property FeralGhoulRace Auto Const
Keyword property BoS100FightGhoulKeyword Auto Const
Keyword property DMP_Combat_HoldPosition_512 Auto Const
Keyword property DMP_Patrol_Run Auto Const
ObjectReference property BoS100Fight_FightCenterMarker Auto Const


Event OnTriggerEnter(ObjectReference akActionRef)
	;Debug.Trace("GHOUL ADDITION TRIGGER: " + akActionRef)
	Actor akActionActor = akActionRef as Actor
	if ((BoS100Fight.GetStage() < 100) && (akActionActor != None) && \
		(!akActionActor.IsDead()) && (akActionActor.GetRace() == FeralGhoulRace) && \
		(!akActionActor.HasKeyword(BoS100FightGhoulKeyword)))
		;Debug.Trace("-ADDING: " + akActionRef)
		AddGhoulToFight(akActionActor)
	Else
		;Debug.Trace("-IGNORING: " + akActionRef)
	EndIf
EndEvent

Function AddGhoulToFight(Actor ghoul)
	ghoul.SetLinkedRef(BoS100Fight_FightCenterMarker)
	ghoul.SetLinkedRef(BoS100Fight_FightCenterMarker, DMP_Combat_HoldPosition_512)
	ghoul.SetLinkedRef(BoS100Fight_FightCenterMarker, DMP_Patrol_Run)
	ghoul.AddKeyword(BoS100FightGhoulKeyword)
	BoS100Fight.AddToAllGhouls(ghoul)
EndFunction
\