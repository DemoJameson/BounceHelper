module BounceHelperBounceFallingBlock

using ..Ahorn, Maple

@mapdef Entity "BounceHelper/BounceZipMover" BounceZipMover(x::Integer, 
                                                            y::Integer, 
                                                            width::Integer=Maple.defaultBlockWidth, 
                                                            height::Integer=Maple.defaultBlockHeight,
                                                            tiletype::Char='3',
                                                            behind::Bool=false,
                                                            climbFall::Bool=true)

const placements = Ahorn.PlacementDict(
    "Falling Block" => Ahorn.EntityPlacement(
        Maple.FallingBlock,
        "rectangle",
        Dict{String, Any}(),
        Ahorn.tileEntityFinalizer
    ),
)

Ahorn.editingOptions(entity::Maple.FallingBlock) = Dict{String, Any}(
    "tiletype" => Ahorn.tiletypeEditingOptions()
)

Ahorn.minimumSize(entity::Maple.FallingBlock) = 8, 8
Ahorn.resizable(entity::Maple.FallingBlock) = true, true

Ahorn.selection(entity::Maple.FallingBlock) = Ahorn.getEntityRectangle(entity)

Ahorn.renderAbs(ctx::Ahorn.Cairo.CairoContext, entity::Maple.FallingBlock, room::Maple.Room) = Ahorn.drawTileEntity(ctx, room, entity)

end