// Source - https://stackoverflow.com/a/49784278
// Posted by AIon, modified by community. See post 'Timeline' for change history
// Retrieved 2026-07-20, License - CC BY-SA 4.0

const express = require('express')
const app = express()
const https = require('https')
const fs = require('fs')
const port = 3000

app.get('/', (req, res) => {
    res.send("IT'S WORKING!")
})

const httpsOptions = {
    key: fs.readFileSync('./security/cert.key'),
    cert: fs.readFileSync('./security/cert.pem')
}
const server = https.createServer(httpsOptions, app)
    .listen(port, () => {
        console.log('server running at ' + port)
    })
