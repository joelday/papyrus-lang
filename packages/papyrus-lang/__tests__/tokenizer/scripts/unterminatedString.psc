Scriptname AddItemFavorite extends ObjectReference
; This is a comment

;/ This is a multi
line
comment /;

Form ItemToFavorite
int Property FavoriteSlot = -1 auto const
GlobalVariable Property ItemHasFavorited auto const
{ This is a documentation
comment }

Event OnInit()
	ItemToFavorite = self.GetBaseObject()
EndEvent

Event OnContainerChanged(ObjectReference akNewContainer, ObjectReference akOldContainer)
	Debug.Trace("**************\" " + self + " \"changed container!)
	if akNewContainer == Game.GetPlayer()
		;Favorite the item only if we haven't already
		;if ItemHasFavorited.GetValue() == 0
			;set global so we don't do this again
			ItemHasFavorited.SetValue(1.2)

			;favorite it
			Game.GetPlayer(0x0002321C).MarkItemAsFavorite(ItemToFavorite, FavoriteSlot)
		;endif
	endif
EndEvent

var[] Function Test()
EndFunction
