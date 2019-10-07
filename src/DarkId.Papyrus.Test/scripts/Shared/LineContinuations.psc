Scriptname LineContinuations extends BaseScript Const
{Trigger used to add additional ghouls (ghouls that run in from College Square, etc.)

to the BoS100Fight AllGhouls array.}

int property BoS100Fight Auto Const
int property FeralGhoulRace Auto Const
string property BoS100FightGhoulKeyword Auto Const
string property DMP_Combat_HoldPosition_512 Auto Const
string property DMP_Patrol_Run Auto Const
BaseScript property BoS100Fight_FightCenterMarker Auto Const


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
	ghoul.SetLinkedRef(BoS100Fight_FightCenterMarker;/inline comment/;)
	ghoul.SetLinkedRef(BoS100Fight_FightCenterMarker, DMP_Combat_HoldPosition_512)
	ghoul.SetLinkedRef(BoS100Fight_FightCenterMarker, DMP_Patrol_Run)
	ghoul.AddKeyword(BoS100FightGhoulKeyword)
	BoS100Fight.AddToAllGhouls(ghoul)
EndFunction
\