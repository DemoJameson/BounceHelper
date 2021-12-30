local drawableNinePatch = require("structs.drawable_nine_patch")
local drawableRectangle = require("structs.drawable_rectangle")
local drawableSprite = require("structs.drawable_sprite")

local BounceHelperBounceMoveBlock = {}

BounceHelperBounceMoveBlock.name = "BounceHelper/BounceMoveBlock"
BounceHelperBounceMoveBlock.depth = 8995
BounceHelperBounceMoveBlock.minimumSize = {16, 16}

local BounceHelperBounceMoveBlockDirections = {
    "Up", "UpRight", "Right", "DownRight", "Down", "DownLeft", "Left", "UpLeft", "Unknown"
}

BounceHelperBounceMoveBlock.placements = {
    {
        name = "normal",
        data = {
            width = 16,
            height = 16,
            direction = "Right",
            speed = 60
        }
    },
    {
        name = "unknown",
        data = {
            width = 16,
            height = 16,
            direction = "Unknown",
            speed = 60
        }
    }
}

local ninePatchOptions = {
    mode = "border",
    borderMode = "repeat"
}

local buttonNinePatchOptions = {
    mode = "fill",
    border = 0
}

local midColor = {4 / 255, 3 / 255, 23 / 255}
local highlightColor = {59 / 255, 50 / 255, 101 / 255}
local buttonColor = {71 / 255, 64 / 255, 112 / 255}

local frameTexture = "objects/moveBlock/base"
local buttonTexture = "objects/moveBlock/button"
local arrowTextures = {
    up = "objects/moveBlock/arrow02",
    upright = "objects/moveBlock/arrow01",
    right = "objects/moveBlock/arrow00",
    downright = "objects/moveBlock/arrow07",
    down = "objects/moveBlock/arrow06",
    downleft = "objects/moveBlock/arrow05",
    left = "objects/moveBlock/arrow04",
    upleft = "objects/moveBlock/arrow03",
    unknown = "objects/BounceHelper/bounceMoveBlock/unknown"
}

function BounceHelperBounceMoveBlock.sprite(room, entity)
    local x, y = entity.x or 0, entity.y or 0
    local width, height = entity.width or 24, entity.height or 24

    local direction = string.lower(entity.direction or "up")

    local blockTexture = frameTexture
    local arrowTexture = arrowTextures[direction] or arrowTextures["up"]

    local ninePatch = drawableNinePatch.fromTexture(blockTexture, ninePatchOptions, x, y, width, height)

    local highlightRectangle = drawableRectangle.fromRectangle("fill", x + 2, y + 2, width - 4, height - 4, highlightColor)
    local midRectangle = drawableRectangle.fromRectangle("fill", x + 8, y + 8, width - 16, height - 16, midColor)

    local arrowSprite = drawableSprite.fromTexture(arrowTexture, entity)
    local arrowSpriteWidth, arrowSpriteHeight = arrowSprite.meta.width, arrowSprite.meta.height
    local arrowX, arrowY = x + math.floor((width - arrowSpriteWidth) / 2), y + math.floor((height - arrowSpriteHeight) / 2)
    local arrowRectangle = drawableRectangle.fromRectangle("fill", arrowX, arrowY, arrowSpriteWidth, arrowSpriteHeight, highlightColor)

    arrowSprite:addPosition(math.floor(width / 2), math.floor(height / 2))

    local sprites = {}

    table.insert(sprites, highlightRectangle:getDrawableSprite())
    table.insert(sprites, midRectangle:getDrawableSprite())

    for _, sprite in ipairs(ninePatch:getDrawableSprite()) do
        table.insert(sprites, sprite)
    end

    table.insert(sprites, arrowRectangle:getDrawableSprite())
    table.insert(sprites, arrowSprite)

    return sprites
end

return BounceHelperBounceMoveBlock